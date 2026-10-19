using System.Text.Json.Nodes;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Analysis;

internal static class AnalysisReadPlan
{
    public static AnalysisReadQuery Resolve(string operation, JsonObject input, DateTimeOffset now, AnalysisHandles handles, string project)
    {
        var window = AnalysisWindow.Resolve(input, now, operation == "list_releases" ? 10080 : 60);
        if ((J.String(input, "cursor") ?? J.String(input, "phase_cursor")) is { } cursor && handles.Read(cursor, project, "cursor")["window"] is JsonObject saved)
            window = AnalysisWindow.Resolve(saved, now);
        var start = window.Start - (window.End - window.Start); var end = window.End;
        void Include(JsonNode? node)
        {
            if (node is not JsonObject obj) return;
            if (J.Date(obj, "start_time") is { } from && from < start) start = from;
            if (J.Date(obj, "end_time") is { } to && to > end) end = to;
            var offset = J.Number(obj, "offset_minutes");
            if (offset > 0 && window.Start.AddMinutes(-offset) < start) start = window.Start.AddMinutes(-offset);
        }
        Include(input["baseline"]); Include(input["group_a"]?["window"]);
        if (input["group_b"] is JsonObject group) Include(group["window"]);
        // group_b previous_window is the window of equal length that precedes group_a's.
        if (J.Text(input["group_b"]) == "previous_window" && input["group_a"]?["window"] is JsonObject cohort
            && J.Date(cohort, "start_time") is { } from && J.Date(cohort, "end_time") is { } to && from - (to - from) < start) start = from - (to - from);
        // A verification's before window is the lookback ahead of its change.
        if (J.Date(input, "since") is { } since && since - (window.End - window.Start) < start) start = since - (window.End - window.Start);
        // A verification's after window runs until now, and a release's first span may be anywhere in retention.
        if (operation == "verify_fix" && now > end) end = now;
        if (operation == "verify_fix" && J.Date(input, "since") is null && J.String(input, "release") is not null) start = DateTimeOffset.UnixEpoch;
        var traces = new HashSet<string>(StringComparer.Ordinal); var sessions = new HashSet<string>(StringComparer.Ordinal);
        void Target(JsonNode? target)
        {
            if (J.String(target, "trace_id") is { } trace) traces.Add(trace);
            if (J.String(target, "session_id") is { } session) sessions.Add(session);
            if (J.String(target, "ci_run_id") is { } ci) sessions.Add(ci);
        }
        Target(input); Target(input["a"]); Target(input["b"]);
        if (operation == "ci_log" && J.String(input["baseline"], "ci_run_id") is { } priorRun) sessions.Add(priorRun);
        // Failure identity is independent of time. These focused queries may span retention,
        // but still have the storage cap and report incomplete coverage instead of false totals.
        if (J.String(input, "failure_id") is not null || operation == "verify_fix" && J.String(input, "verify_id") is not null) start = DateTimeOffset.UnixEpoch;
        if (start < DateTimeOffset.UnixEpoch) start = DateTimeOffset.UnixEpoch;
        return new AnalysisReadQuery
        {
            Start = QylTimeConversions.ToUnixNanoUnsigned(start), End = QylTimeConversions.ToUnixNanoUnsigned(end),
            TraceIds = [.. traces], SessionIds = [.. sessions],
            IncludeWindowWithTargets = operation is "get_failure_context" or "ci_log",
            IncludeLinkTargets = operation == "get_trace",
            IncludeMetricPredecessors = operation is "query_metric" or "run_saved_query",
            // A list of the saved queries reads no telemetry.
            IncludeTelemetry = operation != "run_saved_query" || J.String(input, "id") is not null
        };
    }
}
