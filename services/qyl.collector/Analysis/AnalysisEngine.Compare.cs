using System.Text.Json.Nodes;

namespace Qyl.Collector.Analysis;

internal sealed partial class AnalysisEngine
{
    private static readonly string[] s_targetFields = ["trace_id", "session_id", "failure_id", "ci_run_id"];
    private List<AnalysisRecord> Execution(JsonNode? value)
    {
        var trace = J.String(value, "trace_id"); var session = J.String(value, "session_id");
        if (value?.GetValueKind() == JsonValueKind.String) trace = value.GetValue<string>();
        if (trace is null && session is null) throw new AnalysisInputException("trace_id", "Provide trace_id or session_id.");
        return [.. _spans.Where(s => trace is not null ? s.TraceId == trace : s.SessionId == session)];
    }
    private int Depth(AnalysisRecord span, IReadOnlyList<AnalysisRecord> _)
    {
        if (_depths.TryGetValue((span.TraceId, span.Id), out var cached)) return cached;
        var path = new List<AnalysisRecord>(); var seen = new HashSet<string>(StringComparer.Ordinal); var current = span; var depth = 0;
        while (seen.Add(current.Id))
        {
            ct.ThrowIfCancellationRequested();
            if (_depths.TryGetValue((current.TraceId, current.Id), out var existing)) { depth = existing; break; }
            path.Add(current);
            if (current.Span?.ParentSpanId is not { Length: > 0 } parent || !SpanIndex.TryGetValue((current.TraceId, parent), out var found)) { depth = -1; break; }
            current = found;
        }
        for (var i = path.Count - 1; i >= 0; i--) _depths[(path[i].TraceId, path[i].Id)] = ++depth;
        return _depths.GetValueOrDefault((span.TraceId, span.Id));
    }
    private JsonObject CompareTraces(JsonObject input)
    {
        var result = CompareTracesCore(input);
        result["window"] = _window.Json;
        result["coverage"] ??= Coverage(0, 0, 0, false);
        // Embedded viewer comparisons are complete; the standalone operation applies paging in Run.
        result["has_more"] = false;
        return result;
    }
    private JsonObject CompareTracesCore(JsonObject input)
    {
        var a = Execution(input["a"]); var b = Execution(input["b"]);
        if (a.Count is 0 || b.Count is 0) return J.O(("comparable", false), ("reason", "One execution was not found."), ("aligned", new JsonArray()), ("unmatched", new JsonArray()), ("after_divergence", new JsonArray()), ("first_divergence", null));
        var rootA = a.OrderBy(s => Depth(s, a)).ThenBy(s => s.Start).First(); var rootB = b.OrderBy(s => Depth(s, b)).ThenBy(s => s.Start).First();
        if (rootA.Name != rootB.Name) return J.O(("comparable", false), ("reason", "Root operations differ."), ("aligned", new JsonArray()), ("unmatched", new JsonArray()), ("after_divergence", new JsonArray()), ("first_divergence", null));
        var groupsB = b.GroupBy(s => (s.Service, s.Name, Depth: Depth(s, b))).ToDictionary(g => g.Key, g => new Queue<AnalysisRecord>(g.OrderBy(s => s.Start)));
        var aligned = new List<JsonObject>(); var unmatched = new List<JsonObject>(); JsonObject? first = null;
        foreach (var left in a.OrderBy(s => s.Start))
        {
            ct.ThrowIfCancellationRequested();
            var key = (left.Service, left.Name, Depth: Depth(left, a));
            if (!groupsB.TryGetValue(key, out var queue) || !queue.TryDequeue(out var right))
            { var missing = J.O(("a_span", SpanJson(left)), ("b_span", null), ("kind", "missing")); unmatched.Add(SpanJson(left)); first ??= missing; continue; }
            var differences = new JsonArray();
            if (left.Error != right.Error) differences.Add((JsonNode)J.O(("a_span", SpanJson(left)), ("b_span", SpanJson(right)), ("kind", "status")));
            if (!JsonNode.DeepEquals(left.Attributes, right.Attributes)) differences.Add((JsonNode)J.O(("a_span", SpanJson(left)), ("b_span", SpanJson(right)), ("kind", "attribute")));
            if (Math.Abs(left.Duration - right.Duration) > Math.Max(1, Math.Min(left.Duration, right.Duration) * .1)) differences.Add((JsonNode)J.O(("a_span", SpanJson(left)), ("b_span", SpanJson(right)), ("kind", "duration")));
            var pair = J.O(("a_span", SpanJson(left)), ("b_span", SpanJson(right)), ("differences", differences)); aligned.Add(pair);
            if (differences.Count > 0)
            {
                // Prefer the deepest error boundary over an ancestor carrying propagated ERROR.
                if (first is null || J.String(differences[0], "kind") == "status" && (J.String(first, "kind") != "status" || Depth(left, a) > J.Number(first, "depth")))
                    first = J.O(("a_span", SpanJson(left)), ("b_span", SpanJson(right)), ("kind", J.String(differences[0], "kind")), ("depth", Depth(left, a)));
            }
        }
        foreach (var right in groupsB.Values.SelectMany(q => q)) { var extra = J.O(("a_span", null), ("b_span", SpanJson(right)), ("kind", "extra")); unmatched.Add(SpanJson(right)); first ??= extra; }
        var divergenceTime = first?["a_span"] is { } span ? J.Date(span, "start_time") : null;
        var after = divergenceTime is null ? [] : aligned.Where(pair => J.Date(pair["a_span"], "start_time") > divergenceTime).SelectMany(p => p["differences"]!.AsArray()).ToList();
        first?.Remove("depth");
        return J.O(("comparable", true), ("first_divergence", first), ("aligned", J.A(aligned)), ("unmatched", J.A(unmatched)), ("after_divergence", J.A(after)), ("coverage", Coverage(a.Count + b.Count, a.Count + b.Count, aligned.Count + unmatched.Count, false)));
    }
    private JsonObject FailureContext(JsonObject input)
    {
        var choices = s_targetFields.Count(k => J.String(input, k) is not null);
        if (choices != 1) throw new AnalysisInputException("target", "Provide exactly one of trace_id, session_id, failure_id or ci_run_id.");
        List<AnalysisRecord> spans;
        if (J.String(input, "failure_id") is { } failure)
        {
            var failures = FailureRecords(failure);
            if (failures.Count is 0) throw new AnalysisInputException("failure_id", "Failure was not found in this project.", "failure.not_found");
            spans = [.. _spans.Where(s => s.TraceId == failures.OrderByDescending(item => item.Start).First().TraceId)];
        }
        else if (J.String(input, "ci_run_id") is { } run)
            spans =
            [
                .. _spans.Where(s =>
                    s.SessionId == run || J.Text(s.Value(CollectorSemanticAttributeCatalog.CicdPipelineRunId)) == run)
            ];
        else spans = Execution(input);
        var errors = spans.Where(s => s.Error).ToList();
        // A span id is unique only within its trace, and a session or CI run holds several traces.
        var byId = spans.ToDictionary(static s => (s.TraceId, s.Id));
        var traceIds = spans.Select(s => s.TraceId).ToHashSet(StringComparer.Ordinal);
        var sessionIds = spans.Select(s => s.SessionId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var errorParents = errors.Where(s => s.Span?.ParentSpanId is not null).Select(s => (s.TraceId, s.Span!.ParentSpanId!)).ToHashSet();
        var rootCause = errors.Where(s => !errorParents.Contains((s.TraceId, s.Id))).OrderBy(s => s.Start).FirstOrDefault();
        var selected = errors.Select(static s => (s.TraceId, s.Id)).ToHashSet();
        foreach (var error in errors)
        { var current = error; while (current.Span?.ParentSpanId is { } parent && byId.TryGetValue((current.TraceId, parent), out var ancestor) && selected.Add((ancestor.TraceId, ancestor.Id))) current = ancestor; }
        var maxSpans = PageSize(input, "observed.error_spans"); var maxLogs = PageSize(input, "observed.logs");
        var evidence = spans.Where(s => selected.Contains((s.TraceId, s.Id))).Select(s => { var row = SpanJson(s); row["role"] = s.Error ? "matched" : "context"; return row; }).ToList();
        var logs = _logs.Where(l => traceIds.Contains(l.TraceId) && (l.SpanId is null || byId.ContainsKey((l.TraceId, l.SpanId)))).ToList();
        var missing = spans.Where(s => s.Span?.ParentSpanId is { Length: > 0 } parent && !byId.ContainsKey((s.TraceId, parent))).Select(s => s.Span!.ParentSpanId!).Distinct().ToList();
        var unknowns = missing.Select(id => (JsonNode?)JsonValue.Create("Missing parent span " + id)).ToList();
        if (spans.Any(s => s.End < s.Start)) unknowns.Add(JsonValue.Create("Clock skew: span end precedes start."));
        if (evidence.Count > maxSpans || logs.Count > maxLogs) unknowns.Add(JsonValue.Create("Evidence was truncated."));
        var errorTraces = _spans.Where(s => s.Error).Select(s => s.TraceId).ToHashSet(StringComparer.Ordinal);
        var success = rootCause is null ? null : _spans.Where(s => s.Name == spans.FirstOrDefault()?.Name && !s.Error && !errorTraces.Contains(s.TraceId)).OrderBy(s => Math.Abs((decimal)s.Start - rootCause.Start)).FirstOrDefault();
        JsonObject? divergence = null;
        if (success is not null && spans.FirstOrDefault()?.TraceId is { } failingTrace)
            divergence = CompareTraces(J.O(("a", J.O(("trace_id", failingTrace))), ("b", J.O(("trace_id", success.TraceId)))))["first_divergence"]?.DeepClone().AsObject();
        return J.O(("observed", J.O(("root_cause_span", rootCause is null ? null : SpanJson(rootCause)), ("error_spans", J.A(evidence)),
            ("propagation", J.A(spans.Where(s => s.Span?.ParentSpanId is { } parent && selected.Contains((s.TraceId, parent)) && selected.Contains((s.TraceId, s.Id))).Select(s => J.O(("from", SpanJson(byId[(s.TraceId, s.Span!.ParentSpanId!)])), ("to", SpanJson(s)))))), ("logs", J.A(logs.Select(LogJson))),
            ("retries", J.A(spans.GroupBy(s => (s.TraceId, s.Span?.ParentSpanId, s.Name)).Where(g => g.Count() > 1).Select(g => J.O(("name", g.Key.Name), ("attempts", g.Count()), ("spans", J.A(g.Select(SpanJson))))))),
            ("fanout", J.A(spans.GroupBy(s => (s.TraceId, Parent: s.Span?.ParentSpanId)).Where(g => g.Key.Parent is not null && g.Count() > 1).Select(g => J.O(("parent_span_id", g.Key.Parent), ("name", "children"), ("count", g.Count()), ("trace_id", g.Key.TraceId))))))),
            ("comparison", J.O(("nearest_success_trace_id", success?.TraceId), ("first_divergence", divergence))),
            ("context", J.O(("similar_failures", J.A(errors.GroupBy(Signature).Select(g => J.O(("failure_id", FailureId(g.Key)), ("count", g.Count()), ("first_seen", J.Time(g.Min(s => s.Start))), ("releases", g.Select(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource"))).Distinct()))))), ("changes", Triage(J.O(("service_name", rootCause?.Service)))["regressions"]), ("annotations", J.A(snapshot.Annotations.Where(n => traceIds.Contains(n.TraceId) || n.SessionId is not null && sessionIds.Contains(n.SessionId)).Select(AnnotationJson))))),
            ("settled", spans.Count is 0 || now.ToUnixTimeMilliseconds() - (long)(spans.Max(s => s.End) / 1_000_000) >= 5000), ("unknowns", J.A(unknowns)),
            ("coverage", Coverage(spans.Count + logs.Count, evidence.Count + logs.Count, evidence.Count + logs.Count, false)));
    }
    private JsonArray Discriminators(List<AnalysisRecord> a, List<AnalysisRecord> b, string scope, int limit, HashSet<string>? predicates = null)
    {
        IEnumerable<(string Key, string Value)> Pairs(AnalysisRecord r) => (scope == "resource" ? r.Resource : scope == "span" ? r.Attributes : Merge(r.Attributes, r.Resource)).Select(p => (p.Key, J.Text(p.Value)));
        var excluded = a.Concat(b).SelectMany(Pairs).GroupBy(p => p.Key).Where(g => IsUniqueKey(g.Key) || g.Select(p => p.Value).Distinct().Count() > (a.Count + b.Count) * .8).Select(g => g.Key).ToHashSet();
        var countsA = a.SelectMany(Pairs).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count()); var countsB = b.SelectMany(Pairs).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
        return J.A(countsA.Where(p => !excluded.Contains(p.Key.Key) && !(predicates?.Contains(p.Key.Key) ?? false) && p.Value >= 5).Select(p =>
        { var bc = countsB.GetValueOrDefault(p.Key); var sa = p.Value / (double)Math.Max(1, a.Count); var sb = bc / (double)Math.Max(1, b.Count);
            return J.O(("key", p.Key.Key), ("value", p.Key.Value), ("share_a", sa), ("share_b", sb), ("count_a", p.Value), ("count_b", bc), ("lift", sb is 0 ? null : sa / sb), ("difference", sa - sb)); })
            .OrderByDescending(x => J.Number(x, "difference")).ThenBy(x => J.String(x, "key"), StringComparer.Ordinal).Take(limit));
    }
    private static bool IsUniqueKey(string key) => key.EndsWith(".id", StringComparison.Ordinal) || key.Contains("timestamp", StringComparison.Ordinal) || key.Contains("hash", StringComparison.Ordinal);
    private static JsonObject Merge(JsonObject a, JsonObject b) { var result = a.DeepClone().AsObject(); foreach (var p in b) result[p.Key] ??= p.Value?.DeepClone(); return result; }
    private JsonObject CompareGroups(JsonObject input)
    {
        var signal = J.String(input, "signal") ?? "spans"; var ga = input["group_a"]?.AsObject() ?? new();
        AnalysisWindow WindowOf(JsonObject g) => g["window"] is JsonObject w ? AnalysisWindow.Resolve(w, now) : _window;
        // A cohort holds the request's records inside the cohort's window that satisfy its matchers; a
        // failure id narrows it to that failure's occurrences.
        List<AnalysisRecord> Group(JsonObject g, AnalysisWindow window)
        {
            AnalysisMatchers.Validate(g["matchers"]);
            var records = Select(g, signal, window).Intersect(Select(input, signal, window));
            if (J.String(g, "failure_id") is not { } fid) return [.. records];
            if (signal != "spans") throw new AnalysisInputException("failure_id", "A failure_id cohort holds spans; use signal=spans.");
            var occurrences = FailureRecords(fid).ToHashSet();
            return [.. records.Where(occurrences.Contains)];
        }
        var windowA = WindowOf(ga); var a = Group(ga, windowA); var gb = input["group_b"];
        var b = gb is JsonObject bg ? Group(bg, WindowOf(bg)) : J.Text(gb) == "before" ? Group(ga, new(windowA.Start - (windowA.End - windowA.Start), windowA.Start)) :
            [.. Select(input, signal).Except(a)];
        var scope = J.String(input, "attribute_scope") ?? "both";
        var namesA = a.Select(s => s.Name).ToHashSet(); var namesB = b.Select(s => s.Name).ToHashSet();
        JsonObject Durations(List<AnalysisRecord> rows) => J.O(("p50_ms", J.Percentile(rows.Select(s => s.Duration), .5)), ("p95_ms", J.Percentile(rows.Select(s => s.Duration), .95)), ("p99_ms", J.Percentile(rows.Select(s => s.Duration), .99)));
        var countsA = a.GroupBy(r => r.Name).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var countsB = b.GroupBy(r => r.Name).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        double ShareA(string name) => countsA.GetValueOrDefault(name) / (double)Math.Max(1, a.Count);
        double ShareB(string name) => countsB.GetValueOrDefault(name) / (double)Math.Max(1, b.Count);
        var names = namesA.Union(namesB).Order(StringComparer.Ordinal).ToList();
        var mixChanged = names.Any(name => Math.Abs(ShareA(name) - ShareB(name)) > .01);
        var performanceChanged = !JsonNode.DeepEquals(Durations(a), Durations(b))
            || a.Count(s => s.Error) / (double)Math.Max(1, a.Count) != b.Count(s => s.Error) / (double)Math.Max(1, b.Count);
        var predicates = new[] { ga["matchers"], (gb as JsonObject)?["matchers"] }.OfType<JsonArray>()
            .SelectMany(x => x).Select(x => J.String(x, "key") ?? "").ToHashSet(StringComparer.Ordinal);
        return J.O(
            ("discriminators", Discriminators(a, b, scope, int.MaxValue, predicates)),
            ("structural", J.A(names.Where(name => ShareA(name) != ShareB(name)).Select(name => J.O(("name", name), ("share_a", ShareA(name)), ("share_b", ShareB(name)))))),
            ("durations", J.O(("a", Durations(a)), ("b", Durations(b)))),
            ("composition", J.O(("changed", mixChanged || performanceChanged), ("traffic_mix_changed", mixChanged),
                ("same_traffic_changed", !mixChanged && performanceChanged), ("summary", "These are associations, not causes."))),
            ("size_a", a.Count), ("size_b", b.Count),
            ("excluded_keys", a.Concat(b).SelectMany(s => s.Attributes.Select(p => p.Key)).Where(IsUniqueKey).Distinct()),
            ("coverage", Coverage(a.Count + b.Count, a.Count + b.Count, a.Count + b.Count, false)));
    }

    private JsonObject VerifyFix(JsonObject input)
    {
        if (J.String(input, "verify_id") is { } token) { input = handles.Read(token, project, "verify_id"); _window = AnalysisWindow.Resolve(input, now); }
        var fid = J.String(input, "failure_id"); var matchers = input["matchers"];
        if ((fid is null) == (matchers is null)) throw new AnalysisInputException("failure_id", "Provide exactly one of failure_id or matchers.");
        // One scope selects the requests, and the failures are errors among exactly those requests: the
        // failure's own operation, or every span the matchers select.
        List<AnalysisRecord> requests, failures;
        if (fid is not null)
        {
            failures = FailureRecords(fid);
            if (failures.Count is 0) throw new AnalysisInputException("failure_id", "Failure was not found in this project.", "failure.not_found");
            var operation = failures[0];
            requests = [.. _spans.Where(s => s.Service == operation.Service && Operation(s) == Operation(operation))];
        }
        else
        {
            requests = [.. _spans.Where(s => AnalysisMatchers.Match(s, matchers))];
            failures = [.. requests.Where(static s => s.Error)];
        }
        ulong sinceNs; var sinceKnown = true;
        if (J.Date(input, "since") is { } changed) sinceNs = QylTimeConversions.ToUnixNanoUnsigned(changed);
        else if (J.String(input, "release") is { } release && _spans.Where(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource")) == release)
                     .Select(static s => s.Start).DefaultIfEmpty().Min() is var first and > 0)
        {
            // A release starts at its first span only when no earlier span can be missing from the snapshot.
            sinceNs = first; sinceKnown = SpansComplete(0, first);
        }
        else throw new AnalysisInputException("since", "Provide a change timestamp or an observed release.");
        var nowNs = QylTimeConversions.ToUnixNanoUnsigned(now);
        var elapsed = nowNs > sinceNs ? nowNs - sinceNs : 0;
        // Before the change is the failure's known history from its first occurrence, or the request's lookback.
        var lookback = QylTimeConversions.ToUnixNanoUnsigned(_window.End) - _window.StartNs;
        var beforeNs = fid is not null && failures.Min(static s => s.Start) is var firstSeen && firstSeen < sinceNs ? firstSeen : sinceNs > lookback ? sinceNs - lookback : 0;
        bool After(AnalysisRecord s) => s.Start >= sinceNs && s.Start < nowNs;
        bool Before(AnalysisRecord s) => s.Start >= beforeNs && s.Start < sinceNs;
        var minRequests = J.Number(input, "min_requests", 100); var minMinutes = J.Number(input, "min_minutes", 30);
        var after = requests.Where(After).ToList(); var afterFailures = failures.Where(After).ToList();
        // Only an observed recurrence decides without the whole after window; any other verdict could hide one.
        var afterComplete = sinceKnown && SpansComplete(sinceNs, nowNs);
        var verdict = afterFailures.Count > 0 ? "failed" : !afterComplete ? "inconclusive"
            : after.Count < minRequests || elapsed / 60_000_000_000d < minMinutes ? "pending" : after.Count is 0 ? "inconclusive" : "passed";
        JsonObject Segment(ulong start, ulong end, int occurrences, int count) => J.O(("window", new AnalysisWindow(J.Instant(start), J.Instant(end)).Json), ("occurrences", occurrences), ("requests", count), ("rate", count is 0 ? null : occurrences / (double)count), ("unit", "requests"));
        // A failure is new only against a before window the snapshot holds completely.
        var knownFailures = _spans.Where(s => s.Error && Before(s)).Select(Signature).Concat(failures.Select(Signature)).ToHashSet(StringComparer.Ordinal);
        var guardrails = SpansComplete(beforeNs, sinceNs) ? _spans.Where(s => s.Error && After(s)).GroupBy(Signature).Where(g => !knownFailures.Contains(g.Key))
            .Select(g => { var item = Failure(g); item["change"] = "new"; item["baseline_count"] = 0; item["services"] = J.N(g.Select(s => s.Service).Distinct()); return item; }) : [];
        return J.O(("verdict", verdict), ("before", Segment(beforeNs, sinceNs, failures.Count(Before), requests.Count(Before))), ("after", Segment(sinceNs, nowNs, afterFailures.Count, after.Count)),
            ("needs", verdict == "pending" ? J.O(("requests", Math.Max(0, minRequests - after.Count)), ("minutes", (long)Math.Max(0, Math.Ceiling(minMinutes - elapsed / 60_000_000_000d)))) : null),
            ("recurrences", afterFailures.Select(s => s.TraceId).OfType<string>().Distinct().Take(3)), ("guardrails", J.A(guardrails)), ("verify_id", handles.Issue(project, "verify_id", input)));
    }
}
