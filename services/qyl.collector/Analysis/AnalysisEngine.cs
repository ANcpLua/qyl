using System.Text.Json.Nodes;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Analysis;

internal sealed partial class AnalysisEngine(AnalysisSnapshot snapshot, string project, AnalysisHandles handles, DateTimeOffset now, CancellationToken ct = default)
{
    private readonly List<AnalysisRecord> _spans =
    [
        .. snapshot.Spans.Select(s =>
        {
            ct.ThrowIfCancellationRequested();
            return AnalysisRecord.From(s);
        }).OrderBy(static s => s.Start).ThenBy(static s => s.Id, StringComparer.Ordinal)
    ];
    private readonly List<AnalysisRecord> _logs =
    [
        .. snapshot.Logs.Select(s =>
        {
            ct.ThrowIfCancellationRequested();
            return AnalysisRecord.From(s);
        }).OrderBy(static s => s.Start).ThenBy(static s => s.Id, StringComparer.Ordinal)
    ];
    private Dictionary<(string? Trace, string Id), AnalysisRecord> SpanIndex => field ??= _spans.ToDictionary(static s => (s.TraceId, s.Id));
    private readonly Dictionary<(string? Trace, string Id), int> _depths = [];
    private readonly Dictionary<string, AnalysisStateRow> _state = snapshot.State.ToDictionary(static s => s.StateKey, StringComparer.Ordinal);
    private ILookup<string, MetricPointRow> PointsBySeries => field ??= snapshot.Points.ToLookup(static p => p.SeriesId, StringComparer.Ordinal);
    private AnalysisWindow _window;
    private string _operation = "";

    public JsonObject Run(string operation, JsonObject input)
    {
        ct.ThrowIfCancellationRequested();
        _operation = operation;
        _window = AnalysisWindow.Resolve(input, now, operation == "list_releases" ? 10080 : 60);
        if ((J.String(input, "cursor") ?? J.String(input, "phase_cursor")) is { } continuation)
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
            "get_mcp_stats" => McpStats(input), "list_sessions" => Sessions(input),
            "list_traces" => Traces(input), "get_trace" => Trace(input), "search_logs" => Logs(input),
            "list_metrics" => Metrics(input), "get_metric_series" => MetricSeries(input), "query_metric" => QueryMetric(input),
            "ci_log" => Ci(input), "list_saved_queries" => SavedQueries(input), "run_saved_query" => RunSavedQuery(input), _ => throw new AnalysisInputException("tool", "Unknown analysis operation.")
        };
        result["window"] ??= _window.Json;
        result["coverage"] ??= Coverage(Select(input).Count + Select(input, "logs").Count, Select(input).Count + Select(input, "logs").Count, 1, false);
        return PageCollections(input, result);
    }

    private JsonObject Coverage(int examined, int matched, int returned, bool truncated) => J.O(
        ("examined", examined), ("matched", snapshot.ScanComplete ? matched : null), ("returned", returned), ("truncated", truncated || !snapshot.ScanComplete),
        ("scan_complete", snapshot.ScanComplete), ("as_of", _window.End.ToString("O")));
    // Spans starting in [from, to) are all in the snapshot: a store read reports the range it covered, any
    // other snapshot only when its scan was complete.
    private bool SpansComplete(ulong from, ulong to) => snapshot.SpanCoverage is { } coverage ? coverage.Covers(from, to) : snapshot.ScanComplete;
    private List<AnalysisRecord> Select(JsonObject input, string signal = "spans", AnalysisWindow? window = null)
    {
        var w = window ?? _window;
        return [.. (signal == "logs" ? _logs : _spans).Where(r => Checked(r) && w.Contains(r.Start) && InScope(input, r))];
    }
    // The request's service, environment, release and matcher filters.
    private static bool InScope(JsonObject input, AnalysisRecord r) =>
        (J.String(input, "service_name") is not { } service || r.Service == service)
        && (J.String(input, "environment") is not { } environment
            || J.Text(r.Value(CollectorSemanticAttributeCatalog.DeploymentEnvironmentName, "resource")) == environment
            || J.Text(r.Value(CollectorSemanticAttributeCatalog.DeploymentEnvironment, "resource")) == environment)
        && (J.String(input, "release") is not { } version || J.Text(r.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource")) == version)
        && AnalysisMatchers.Match(r, input["matchers"]);
    private bool Checked(AnalysisRecord _) { ct.ThrowIfCancellationRequested(); return true; }
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
    private static string LogBody(AnalysisRecord s) => ContractJson.ParseLogBody(s.Log?.Body) switch
    {
        Api.Contracts.Common.AttributeValue.StringValue text => text.Value,
        null => "",
        var value => JsonSerializer.Serialize(value, QylSerializerContext.Default.AttributeValue)
    };
    private static string Message(AnalysisRecord s) => s.Span?.StatusMessage ?? LogBody(s);
    private static string Operation(AnalysisRecord s) => J.Text(s.Value(CollectorSemanticAttributeCatalog.HttpRoute)) is { Length: > 0 } route ? route : s.Name;
    private static string Signature(AnalysisRecord s) => string.Join("|", s.Service, Operation(s), FailureKind(s), J.Normalize(Message(s)));
    private string FailureId(string signature) => FailureIdentity(project, signature);
    private static string FailureIdentity(string projectId, string signature) => "fail_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(projectId + "\0" + signature)));
    internal static string? FailureIdentity(string projectId, SpanStorageRow row) => row.StatusCode == 2 ? FailureIdentity(projectId, Signature(AnalysisRecord.From(row))) : null;
    private JsonObject State(string id) => _state.GetValueOrDefault(id) is { } row
        ? JsonNode.Parse(row.ValueJson)!.AsObject() : J.O(("status", "open"));
    private string Etag(string id) => _state.GetValueOrDefault(id)?.Etag ?? '"' + id + '"';
    private List<AnalysisRecord> FailureRecords(string id) =>
        [.. _spans.Where(s => s.Error && FailureId(Signature(s)) == id)];
    public bool OwnsFailure(string id) => FailureRecords(id).Count > 0;
    private static AnalysisInputException NoFailure(string id) =>
        new("failure_id", $"Failure {id} was not found in this project; triage and list_failures list the failures.", "failure.not_found") { Missing = id };
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
            ("status", J.String(state, "status") ?? "open"), ("updated_at", J.String(state, "updated_at") ?? J.Time(records.Min(s => s.Start))), ("ignore_until", state["ignore_until"]), ("assignee", state["assignee"]),
            ("annotation_count", notes), ("example_trace_ids", records.Select(s => s.TraceId).OfType<string>().Distinct().Take(3)),
            ("raw_messages", records.Select(Message).Distinct().Take(3)));
    }
    private bool AnnotationTargetsFailure(AnnotationRow note, string id, List<AnalysisRecord> records)
    {
        var metadata = _state.GetValueOrDefault("annotation:" + note.AnnotationId);
        if (metadata is not null && J.String(JsonNode.Parse(metadata.ValueJson)?["target"], "failure_id") == id) return true;
        return note.TargetKind switch { "trace" => records.Any(r => r.TraceId == note.TraceId), "span" => records.Any(r => r.SpanId == note.SpanId && r.TraceId == note.TraceId), "session" => records.Any(r => r.SessionId == note.SessionId), _ => false };
    }
    private JsonObject ListFailures(JsonObject input)
    {
        var records = Select(input).Where(s => s.Error).ToList();
        if (J.String(input, "failure_id") is { } id)
        {
            if (!OwnsFailure(id)) throw NoFailure(id);
            records = [.. records.Where(s => FailureId(Signature(s)) == id)];
        }
        var failures = records.GroupBy(Signature).Select(Failure).Where(f => J.String(input, "status") == "all" || J.String(f, "status") == (J.String(input, "status") ?? "open"));
        failures = J.String(input, "sort_by") switch
        {
            "first_seen" => failures.OrderBy(f => J.String(f, "first_seen"), StringComparer.Ordinal),
            "last_seen" => failures.OrderByDescending(f => J.String(f, "last_seen"), StringComparer.Ordinal),
            "affected_traces" => failures.OrderByDescending(f => J.Number(f, "affected_traces")),
            _ => failures.OrderByDescending(f => J.Number(f, "count"))
        };
        var result = Page(input, "failures", failures.ThenByStable());
        if (J.String(input, "failure_id") is not null)
        {
            if (result["failures"]?.AsArray().FirstOrDefault()?.AsObject() is not { } detail) return result;
            detail["counts_per_release"] = J.A(records.GroupBy(r => J.Text(r.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource"))).Select(g => J.O(("release", g.Key), ("count", g.Count()))));
            detail["recent_occurrences"] = J.A(records.OrderByDescending(r => r.Start).Take(3).Select(r => J.O(("trace_id", r.TraceId), ("span_id", r.Id), ("session_id", r.SessionId), ("timestamp", J.Time(r.Start)), ("message", Message(r)), ("release", J.Text(r.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource"))))));
            detail["discriminators"] = Discriminators(records, [.. Select(input).Except(records)], "both", 10);
        }
        return result;
    }
    private JsonObject Triage(JsonObject input)
    {
        var current = Select(input); var before = Select(input, window: Baseline(input));
        var releases = ReleaseList([.. current, .. before]);
        var baselineOperations = before.GroupBy(s => (s.Service, Op: Operation(s))).ToDictionary(g => g.Key, g => g.ToList());
        var errors = current.Where(s => s.Error).GroupBy(Signature); var baseline = before.Where(s => s.Error).GroupBy(Signature).ToDictionary(g => g.Key, g => g.Count());
        var failures = errors.Select(g =>
        {
            var f = Failure(g); var id = J.String(f, "failure_id")!; var state = State(id);
            var old = baseline.GetValueOrDefault(g.Key); var count = g.Count(); var status = J.String(state, "status");
            var changedAfter = J.Date(state, "updated_at");
            var change = status == "resolved" && changedAfter.HasValue && g.Any(s => s.Start > QylTimeConversions.ToUnixNanoUnsigned(changedAfter.Value))
                ? "regressed" : old is 0 ? "new" : count > old ? "worsening" : count < old ? "recovering" : "ongoing";
            f["change"] = change; f["baseline_count"] = old;
            f["services"] = J.N(g.Select(s => s.Service).Distinct()); f["example_trace_id"] = g.First().TraceId;
            f["coinciding_release"] = CoincidingRelease(g, releases)?.DeepClone();
            f["root_cause_failure_id"] = RootCauseFailure(g);
            return f;
        }).Where(f => J.String(f, "status") != "ignored" || J.Date(f, "ignore_until") is { } expiry && expiry <= now)
          .OrderByDescending(f => J.Number(f, "count")).ThenBy(f => J.String(f, "failure_id"), StringComparer.Ordinal).ToList();
        var regressions = new List<JsonObject>();
        foreach (var g in current.GroupBy(s => (s.Service, Op: Operation(s))))
        {
            var b = baselineOperations.GetValueOrDefault(g.Key); if (b is null || b.Count < 20 || g.Count() < 20) continue;
            var rate = g.Count(s => s.Error) / (double)g.Count(); var oldRate = b.Count(s => s.Error) / (double)b.Count;
            foreach (var m in new[] { ("error_rate", rate, oldRate), ("p95_ms", J.Percentile(g.Select(s => s.Duration), .95), J.Percentile(b.Select(s => s.Duration), .95)), ("throughput", g.Count(), b.Count) })
                if (m.Item1 == "throughput" ? m.Item2 < m.Item3 * .8 : m.Item1 == "error_rate" ? m.Item2 >= m.Item3 + .05 : m.Item2 > Math.Max(m.Item3 * 1.2, m.Item3 + 10))
                    regressions.Add(J.O(("service", g.Key.Service), ("operation", g.Key.Op), ("measure", m.Item1), ("value", m.Item2), ("baseline_value", m.Item3), ("denominator", g.Count()), ("unit", m.Item1 == "p95_ms" ? "ms" : m.Item1 == "error_rate" ? "1" : "spans")));
        }
        // New failures lead the ranking, so they keep their places on the first page even when their count is low.
        return J.O(("failures", J.A(failures.OrderBy(f => J.String(f, "change") == "new" ? 0 : 1))), ("regressions", J.A(regressions)), ("releases", J.A(releases.Where(r => J.Date(r, "first_seen") >= _window.Start))),
            ("silent_services", Silent(input)), ("coverage", Coverage(_spans.Count, current.Count, failures.Count, false)));
    }
    // The release the failing spans run on: their most common version, the later one among equals, when the
    // release was first seen in the window.
    private JsonObject? CoincidingRelease(IEnumerable<AnalysisRecord> failing, List<JsonObject> releases)
    {
        if (failing.Where(static s => ReleaseOf(s).Length > 0).GroupBy(static s => (s.Service, Version: ReleaseOf(s), Env: EnvironmentOf(s)))
                .OrderByDescending(static g => g.Count()).ThenByDescending(static g => g.Max(static s => s.Start)).FirstOrDefault()?.Key is not { } run) return null;
        return releases.FirstOrDefault(r => J.String(r, "service") == run.Service && J.String(r, "version") == run.Version && J.String(r, "environment") == run.Env
            && _window.Contains(QylTimeConversions.ToUnixNanoUnsigned(J.Date(r, "first_seen")!.Value)));
    }
    private ILookup<string?, AnalysisRecord> ErrorsByTrace => field ??= _spans.Where(static s => s.Error).ToLookup(static s => s.TraceId);
    // The failure the occurrences propagated from: in each of their traces the earliest error span without a failing
    // child, by majority.
    private string? RootCauseFailure(IEnumerable<AnalysisRecord> occurrences) => occurrences.Select(static s => s.TraceId).Distinct().Select(trace =>
        {
            var errors = ErrorsByTrace[trace].ToList();
            var parents = errors.Select(static s => s.Span?.ParentSpanId).OfType<string>().ToHashSet(StringComparer.Ordinal);
            return errors.Where(s => !parents.Contains(s.Id)).OrderBy(static s => s.Start).ThenBy(static s => s.Id, StringComparer.Ordinal).FirstOrDefault();
        }).OfType<AnalysisRecord>().GroupBy(s => FailureId(Signature(s))).OrderByDescending(static g => g.Count()).ThenBy(static g => g.Key, StringComparer.Ordinal).FirstOrDefault()?.Key;
    // A service is silent when its last span over the baseline and the window is older than the window's end minus
    // 15 minutes or five of its usual intervals between spans, whichever is longer.
    private JsonArray Silent(JsonObject input)
    {
        var baseline = Baseline(input); var minutes = (baseline.End - baseline.Start + (_window.End - _window.Start)).TotalMinutes;
        var end = (double)QylTimeConversions.ToUnixNanoUnsigned(_window.End);
        return J.A(Select(input, window: baseline).Concat(Select(input)).DistinctBy(static s => (s.TraceId, s.Id)).GroupBy(static s => s.Service)
            .Select(static g =>
            {
                var starts = g.Select(static s => s.Start).Order().ToList();
                var usual = starts.Count > 1 ? (starts[^1] - starts[0]) / (double)(starts.Count - 1) : 0;
                return (Service: g.Key, Last: starts[^1], Count: starts.Count, Quiet: Math.Max(15 * 60_000_000_000d, 5 * usual));
            })
            .Where(s => s.Last < end - s.Quiet).OrderBy(static s => s.Service, StringComparer.Ordinal)
            .Select(s => J.O(("service", s.Service), ("last_seen", J.Time(s.Last)), ("usual_rate", s.Count / minutes), ("unit", "spans/minute"))));
    }
    private static JsonObject SpanJson(AnalysisRecord r)
    {
        var result = J.O(("span_id", r.SpanId), ("trace_id", r.TraceId), ("session_id", r.SessionId),
        ("service", r.Service), ("name", r.Name), ("start_time", J.Time(r.Start)), ("end_time", J.Time(r.End)), ("duration_ms", r.Duration),
        ("status", r.Error ? "ERROR" : "OK"), ("status_message", r.Span?.StatusMessage), ("parent_span_id", r.Span?.ParentSpanId),
        ("key_attributes", ContractAttributes(r.Attributes)), ("exception", J.O(("type", FailureKind(r)), ("message", Message(r)))), ("role", r.Error ? "matched" : "context"));
        foreach (var key in result.Where(static p => p.Value is null || p.Key == "parent_span_id" && J.Text(p.Value).Length is 0).Select(static p => p.Key).ToArray()) result.Remove(key);
        return result;
    }
}

internal static class AnalysisSortExtensions
{
    public static IEnumerable<JsonObject> ThenByStable(this IEnumerable<JsonObject> source) => source is IOrderedEnumerable<JsonObject> ordered
        ? ordered.ThenBy(static f => J.String(f, "failure_id"), StringComparer.Ordinal) : source.OrderBy(static f => J.String(f, "failure_id"), StringComparer.Ordinal);
}
