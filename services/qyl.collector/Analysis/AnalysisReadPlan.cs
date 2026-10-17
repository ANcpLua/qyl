using System.Text.Json.Nodes;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Analysis;

internal static class AnalysisReadPlan
{
    public static AnalysisReadQuery Resolve(string operation, JsonObject input, DateTimeOffset now, AnalysisHandles handles, string project)
    {
        var window = AnalysisWindow.Resolve(input, now, operation == "list_releases" ? 10080 : J.Number(input, "hours", 1) * 60);
        if ((J.String(input, "cursor") ?? J.String(input, "phase_cursor")) is { } cursor && handles.Read(cursor, project, "cursor")["window"] is JsonObject saved)
            window = AnalysisWindow.Resolve(saved, now);
        var start = window.Start - (window.End - window.Start); var end = window.End;
        void Include(JsonNode? node)
        {
            if (node is not JsonObject obj) return;
            if (J.Date(obj, "start_time") is { } from && from < start) start = from;
            if (J.Date(obj, "end_time") is { } to && to > end) end = to;
            var offset = J.Number(obj, "offset_minutes") + J.Number(obj, "offset_ms") / 60000;
            if (offset > 0 && window.Start.AddMinutes(-offset) < start) start = window.Start.AddMinutes(-offset);
        }
        Include(input["baseline"]); Include(input["compare_to"]); Include(input["group_a"]?["window"]);
        if (input["group_b"] is JsonObject group) Include(group["window"]);
        if (J.Date(input, "since") is { } since && since - (now - since) < start) start = since - (now - since);
        var traces = new HashSet<string>(StringComparer.Ordinal); var sessions = new HashSet<string>(StringComparer.Ordinal);
        void Target(JsonNode? target)
        {
            if (J.String(target, "trace_id") is { } trace) traces.Add(trace);
            if (J.String(target, "session_id") is { } session) sessions.Add(session);
            if (J.String(target, "run_id") is { } run) sessions.Add(run);
            if (J.String(target, "ci_run_id") is { } ci) sessions.Add(ci);
        }
        Target(input); Target(input["a"]); Target(input["b"]);
        if (operation == "display_traces" && J.String(input, "compare_to") is { } comparisonTrace) traces.Add(comparisonTrace);
        if (operation == "ci_log" && J.String(input, "compare_to") is { } priorRun && priorRun != "last_success") sessions.Add(priorRun);
        // Failure identity is independent of time. These focused queries may span retention,
        // but still have the storage cap and report incomplete coverage instead of false totals.
        if (J.String(input, "failure_id") is not null || operation == "verify_fix" && J.String(input, "verify_id") is not null) start = DateTimeOffset.UnixEpoch;
        if (start < DateTimeOffset.UnixEpoch) start = DateTimeOffset.UnixEpoch;
        return new AnalysisReadQuery
        {
            Start = QylTimeConversions.ToUnixNanoUnsigned(start), End = QylTimeConversions.ToUnixNanoUnsigned(end),
            TraceIds = [.. traces], SessionIds = [.. sessions],
            IncludeWindowWithTargets = operation is "get_failure_context" or "ci_log",
            IncludeTelemetry = operation != "list_saved_queries"
        };
    }
}
