using System.Text;
using System.Text.Json.Nodes;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Analysis;

internal sealed partial class AnalysisEngine(AnalysisSnapshot snapshot, string project, AnalysisHandles handles, DateTimeOffset now)
{
    private readonly List<AnalysisRecord> _spans = snapshot.Spans.Select(AnalysisRecord.From).OrderBy(static s => s.Start).ThenBy(static s => s.Id, StringComparer.Ordinal).ToList();
    private readonly List<AnalysisRecord> _logs = snapshot.Logs.Select(AnalysisRecord.From).OrderBy(static s => s.Start).ThenBy(static s => s.Id, StringComparer.Ordinal).ToList();
    private AnalysisWindow _window;
    private string _operation = "";

    public JsonObject Run(string operation, JsonObject input)
    {
        _operation = operation;
        _window = AnalysisWindow.Resolve(input, now, operation == "list_releases" ? 10080 : J.Number(input, "hours") > 0 ? J.Number(input, "hours") * 60 : 60);
        if (J.String(input, "cursor") is { } continuation)
        {
            var state = handles.Read(continuation, project, "cursor");
            if (state["window"] is JsonObject window) _window = AnalysisWindow.Resolve(window, now);
        }
        AnalysisMatchers.Validate(input["matchers"]);
        var result = operation switch
        {
            "triage" => Triage(input), "list_failures" => ListFailures(input), "get_failure_context" => FailureContext(input),
            "compare_groups" => CompareGroups(input), "compare_traces" => CompareTraces(input), "verify_fix" => VerifyFix(input),
            "aggregate_telemetry" => Aggregate(input), "discover_attributes" => Discover(input), "get_agent_run" => AgentRun(input),
            "list_releases" => Releases(input), "get_service_map" => ServiceMap(input), "check_telemetry_health" => Health(input),
            "get_mcp_stats" => McpStats(input), "display_mcp_dashboard" => McpDashboard(input), "list_sessions" => Sessions(input),
            "list_traces" or "display_traces" => Traces(input), "get_trace" => Trace(input), "search_logs" => Logs(input),
            "list_metrics" => Metrics(input), "get_metric_series" => MetricSeries(input), "query_metric" => QueryMetric(input),
            "ci_log" => Ci(input), "list_saved_queries" => SavedQueries(input), "run_saved_query" => RunSavedQuery(input), "fetch_telemetry" => Fetch(input), _ => throw new AnalysisInputException("tool", "Unknown analysis operation.")
        };
        result["window"] ??= _window.Json;
        result["coverage"] ??= Coverage(_spans.Count + _logs.Count, _spans.Count + _logs.Count, 1, false);
        return result;
    }

    private JsonObject Coverage(int examined, int matched, int returned, bool truncated) => J.O(
        ("examined", examined), ("matched", matched), ("returned", returned), ("truncated", truncated),
        ("scan_complete", true), ("as_of", now.ToString("O")));
    private List<AnalysisRecord> Select(JsonObject input, string signal = "spans", AnalysisWindow? window = null)
    {
        var w = window ?? _window;
        return (signal == "logs" ? _logs : _spans).Where(r => w.Contains(r.Start)
            && (J.String(input, "service_name") is not { } svc || r.Service == svc)
            && (J.String(input, "environment") is not { } env || J.Text(r.Value(CollectorSemanticAttributeCatalog.DeploymentEnvironmentName, "resource")) == env || J.Text(r.Value(CollectorSemanticAttributeCatalog.DeploymentEnvironment, "resource")) == env)
            && (J.String(input, "release") is not { } version || J.Text(r.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource")) == version)
            && AnalysisMatchers.Match(r, input["matchers"])).ToList();
    }
    private JsonObject Page(JsonObject input, string field, IEnumerable<JsonObject> source, int defaultLimit = 100, int max = 1000)
    {
        var all = source.ToList(); var limit = (int)J.Number(input, "limit", defaultLimit);
        if (limit < 1 || limit > max) throw new AnalysisInputException("limit", $"limit must be between 1 and {max}.", "limit.out_of_range");
        var offset = 0; var scope = input.DeepClone().AsObject(); scope.Remove("cursor"); scope.Remove("limit");
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(scope.ToJsonString())));
        if (J.String(input, "cursor") is { } cursor)
        {
            var value = handles.Read(cursor, project, "cursor");
            if (J.String(value, "operation") != _operation || J.String(value, "filter") != fingerprint)
                throw new AnalysisInputException("cursor", "Use this cursor with its original operation and filters.", "cursor.invalid");
            var anchor = J.String(value, "after");
            offset = all.FindIndex(row => PageIdentity(row) == anchor) + 1;
            if (offset == 0) throw new AnalysisInputException("cursor", "The continuation anchor is no longer retained. Start a new query.", "cursor.expired");
        }
        var page = all.Skip(offset).Take(limit).ToList(); var more = offset + page.Count < all.Count;
        return J.O((field, J.A(page)), ("has_more", more), ("next_cursor", more ? handles.Issue(project, "cursor", J.O(("operation", _operation), ("filter", fingerprint), ("after", PageIdentity(page[^1])), ("window", _window.Json))) : null),
            ("coverage", Coverage(_spans.Count + _logs.Count, all.Count, page.Count, more)));
    }
    private static string PageIdentity(JsonObject row)
    {
        foreach (var key in new[] { "failure_id", "session_id", "trace_id", "log_id", "id", "saved_query_id", "series_id", "name", "run_id" })
            if (J.String(row, key) is { } id) return key + ":" + id;
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(row.ToJsonString())));
    }
    private AnalysisWindow Baseline(JsonObject input)
    {
        if (input["baseline"] is JsonObject custom)
        {
            if (custom["offset_minutes"] is not null) { var offset = J.Number(custom, "offset_minutes"); return new(_window.Start.AddMinutes(-offset), _window.End.AddMinutes(-offset)); }
            return AnalysisWindow.Resolve(custom, _window.Start);
        }
        var offsetMinutes = J.Number(input, "offset_minutes");
        if (offsetMinutes > 0) return new(_window.Start.AddMinutes(-offsetMinutes), _window.End.AddMinutes(-offsetMinutes));
        return new(_window.Start - (_window.End - _window.Start), _window.Start);
    }
    private static string FailureKind(AnalysisRecord s) => J.Text(s.Value(CollectorSemanticAttributeCatalog.RecordedFailureKind)) is { Length: > 0 } ex ? ex : J.Text(s.Value(CollectorSemanticAttributeCatalog.ErrorType));
    private static string LogBody(AnalysisRecord s) => s.Log is null ? "" : J.Text(JsonNode.Parse(s.Log.Body ?? "null"));
    private static string Message(AnalysisRecord s) => J.Text(s.Value(CollectorSemanticAttributeCatalog.ExceptionMessage)) is { Length: > 0 } ex ? ex : s.Span?.StatusMessage ?? LogBody(s);
    private static string Operation(AnalysisRecord s) => J.Text(s.Value(CollectorSemanticAttributeCatalog.HttpRoute)) is { Length: > 0 } route ? route : s.Name;
    private static string Signature(AnalysisRecord s) => string.Join("|", s.Service, Operation(s), FailureKind(s), J.Normalize(Message(s)));
    private string FailureId(string signature) => "fail_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(project + "\0" + signature)));
    private JsonObject State(string id) => snapshot.State.FirstOrDefault(s => s.StateKey == id) is { } row
        ? JsonNode.Parse(row.ValueJson)!.AsObject() : J.O(("status", "open"));
    private string Etag(string id) => snapshot.State.FirstOrDefault(s => s.StateKey == id)?.Etag ?? '"' + id + '"';
    private List<AnalysisRecord> FailureRecords(string id) => _spans.Where(s => s.Error && FailureId(Signature(s)) == id).ToList();
    public bool OwnsFailure(string id) => FailureRecords(id).Count > 0;
    private JsonObject Failure(IGrouping<string, AnalysisRecord> group)
    {
        var records = group.ToList(); var first = records[0]; var id = FailureId(group.Key); var state = State(id);
        var notes = snapshot.Annotations.Count(n => AnnotationTargetsFailure(n, id, records));
        return J.O(("failure_id", id), ("etag", Etag(id)), ("signature", group.Key), ("exception_type", FailureKind(first)),
            ("normalized_message", J.Normalize(Message(first))), ("operation", Operation(first)), ("service", first.Service),
            ("first_seen", J.Time(records.Min(s => s.Start))), ("last_seen", J.Time(records.Max(s => s.Start))),
            ("count", records.Count), ("affected_traces", records.Select(s => s.TraceId).Distinct().Count()),
            ("affected_sessions", records.Select(s => s.SessionId).Where(s => s is not null).Distinct().Count()),
            ("releases_seen", records.Select(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource"))).Where(s => s.Length > 0).Distinct()),
            ("status", J.String(state, "status") ?? "open"), ("ignore_until", state["ignore_until"]), ("assignee", state["assignee"]),
            ("annotation_count", notes), ("example_trace_ids", records.Select(s => s.TraceId).OfType<string>().Distinct().Take(3)),
            ("raw_messages", records.Select(Message).Distinct().Take(3)));
    }
    private bool AnnotationTargetsFailure(AnnotationRow note, string id, List<AnalysisRecord> records)
    {
        var metadata = snapshot.State.FirstOrDefault(s => s.StateKey == "annotation:" + note.AnnotationId);
        if (metadata is not null && J.String(JsonNode.Parse(metadata.ValueJson)?["target"], "failure_id") == id) return true;
        return note.TargetKind switch { "trace" => records.Any(r => r.TraceId == note.TraceId), "span" => records.Any(r => r.SpanId == note.SpanId && r.TraceId == note.TraceId), "session" => records.Any(r => r.SessionId == note.SessionId), _ => false };
    }
    private JsonObject ListFailures(JsonObject input)
    {
        var records = Select(input).Where(s => s.Error).ToList();
        if (J.String(input, "failure_id") is { } id)
        {
            if (!OwnsFailure(id)) throw new AnalysisInputException("failure_id", "Failure was not found in this project.", "failure.not_found");
            records = records.Where(s => FailureId(Signature(s)) == id).ToList();
        }
        var failures = records.GroupBy(Signature).Select(Failure).Where(f => J.String(input, "status") == "all" || J.String(f, "status") == (J.String(input, "status") ?? "open"));
        failures = J.String(input, "sort_by") switch
        {
            "first_seen" => failures.OrderBy(f => J.String(f, "first_seen"), StringComparer.Ordinal),
            "last_seen" => failures.OrderByDescending(f => J.String(f, "last_seen"), StringComparer.Ordinal),
            "affected_traces" => failures.OrderByDescending(f => J.Number(f, "affected_traces")),
            _ => failures.OrderByDescending(f => J.Number(f, "count"))
        };
        var result = Page(input, "failures", failures.ThenByStable(), 100);
        if (J.String(input, "failure_id") is not null)
        {
            var detail = result["failures"]?.AsArray().FirstOrDefault()?.AsObject();
            if (detail is null) return result;
            detail["counts_per_release"] = J.A(records.GroupBy(r => J.Text(r.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource"))).Select(g => J.O(("release", g.Key), ("count", g.Count()))));
            detail["recent_occurrences"] = J.A(records.OrderByDescending(r => r.Start).Take(3).Select(r => J.O(("trace_id", r.TraceId), ("span_id", r.Id), ("session_id", r.SessionId), ("timestamp", J.Time(r.Start)), ("message", Message(r)), ("release", J.Text(r.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource"))))));
            detail["discriminators"] = Discriminators(records, Select(input).Except(records).ToList(), "both", 10);
        }
        return result;
    }
    private JsonObject Triage(JsonObject input)
    {
        var current = Select(input); var before = Select(input, window: Baseline(input));
        var errors = current.Where(s => s.Error).GroupBy(Signature); var baseline = before.Where(s => s.Error).GroupBy(Signature).ToDictionary(g => g.Key, g => g.Count());
        var failures = errors.Select(g =>
        {
            var f = Failure(g); var id = J.String(f, "failure_id")!; var state = State(id);
            var old = baseline.GetValueOrDefault(g.Key); var count = g.Count(); var status = J.String(state, "status");
            var changedAfter = J.Date(state, "updated_at");
            var change = status == "resolved" && changedAfter.HasValue && g.Any(s => s.Start > (ulong)changedAfter.Value.ToUnixTimeMilliseconds() * 1_000_000)
                ? "regressed" : old == 0 ? "new" : count > old ? "worsening" : count < old ? "recovering" : "ongoing";
            f["change"] = change; f["baseline_count"] = old;
            f["services"] = J.N(g.Select(s => s.Service).Distinct()); f["example_trace_id"] = g.First().TraceId;
            var release = ReleaseList(current.Concat(before).ToList()).Where(r => J.String(r, "service") == g.First().Service && _window.Contains((ulong)(J.Date(r, "first_seen")!.Value.ToUnixTimeMilliseconds()) * 1_000_000)).OrderBy(r => J.String(r, "first_seen"), StringComparer.Ordinal).LastOrDefault();
            f["coinciding_release"] = release?.DeepClone();
            return f;
        }).Where(f => J.String(f, "status") != "ignored" || J.Date(f, "ignore_until") is { } expiry && expiry <= now)
          .OrderByDescending(f => J.Number(f, "count")).ThenBy(f => J.String(f, "failure_id"), StringComparer.Ordinal).ToList();
        var limit = (int)J.Number(input, "limit", 10); if (limit < 1 || limit > 50) throw new AnalysisInputException("limit", "limit must be between 1 and 50.");
        // New failures have reserved slots even when their count is low.
        var kept = failures.Where(f => J.String(f, "change") == "new").ToList();
        kept.AddRange(failures.Where(f => J.String(f, "change") != "new").Take(Math.Max(0, limit - kept.Count)));
        var regressions = new List<JsonObject>();
        foreach (var g in current.GroupBy(s => (s.Service, Op: Operation(s))))
        {
            var b = before.Where(s => s.Service == g.Key.Service && Operation(s) == g.Key.Op).ToList(); if (b.Count == 0) continue;
            var rate = g.Count(s => s.Error) / (double)g.Count(); var oldRate = b.Count(s => s.Error) / (double)b.Count;
            foreach (var m in new[] { ("error_rate", rate, oldRate), ("p95_ms", J.Percentile(g.Select(s => s.Duration), .95), J.Percentile(b.Select(s => s.Duration), .95)), ("throughput", (double)g.Count(), b.Count) })
                if (m.Item1 == "throughput" ? m.Item2 < m.Item3 : m.Item2 > m.Item3)
                    regressions.Add(J.O(("service", g.Key.Service), ("operation", g.Key.Op), ("measure", m.Item1), ("value", m.Item2), ("baseline_value", m.Item3), ("denominator", g.Count()), ("unit", m.Item1 == "p95_ms" ? "ms" : m.Item1 == "error_rate" ? "1" : "spans")));
        }
        return J.O(("failures", J.A(kept)), ("regressions", J.A(regressions.Take(limit))), ("releases", J.A(ReleaseList(_spans).Where(r => J.Date(r, "first_seen") >= _window.Start).Take(limit))),
            ("silent_services", Silent(input)), ("coverage", Coverage(_spans.Count, current.Count, kept.Count, kept.Count < failures.Count || regressions.Count > limit)));
    }
    private JsonArray Silent(JsonObject input) => J.A(_spans.GroupBy(s => s.Service)
        .Where(g => (!input.ContainsKey("service_name") || g.Key == J.String(input, "service_name")) && g.Max(s => s.Start) < _window.StartNs)
        .Select(g => J.O(("service", g.Key), ("last_seen", J.Time(g.Max(s => s.Start))), ("usual_rate", g.Count() / Math.Max(1d, (g.Max(s => s.Start) - g.Min(s => s.Start)) / 60_000_000_000d)), ("unit", "spans/minute"))));
    private static JsonObject SpanJson(AnalysisRecord r)
    {
        var result = J.O(("span_id", r.SpanId), ("trace_id", r.TraceId), ("session_id", r.SessionId),
        ("service", r.Service), ("name", r.Name), ("start_time", J.Time(r.Start)), ("end_time", J.Time(r.End)), ("duration_ms", r.Duration),
        ("status", r.Error ? "ERROR" : "OK"), ("status_message", r.Span?.StatusMessage), ("parent_span_id", r.Span?.ParentSpanId),
        ("key_attributes", ContractAttributes(r.Attributes)), ("exception", J.O(("type", FailureKind(r)), ("message", Message(r)))), ("role", r.Error ? "matched" : "context"));
        foreach (var key in result.Where(static p => p.Value is null || p.Key == "parent_span_id" && J.Text(p.Value).Length == 0).Select(static p => p.Key).ToArray()) result.Remove(key);
        return result;
    }
}

internal static class AnalysisSortExtensions
{
    public static IEnumerable<JsonObject> ThenByStable(this IEnumerable<JsonObject> source) => source is IOrderedEnumerable<JsonObject> ordered
        ? ordered.ThenBy(static f => J.String(f, "failure_id"), StringComparer.Ordinal) : source.OrderBy(static f => J.String(f, "failure_id"), StringComparer.Ordinal);
}
