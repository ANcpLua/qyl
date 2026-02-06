using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using qyl.collector.Storage;
using qyl.copilot.Instrumentation;

namespace qyl.collector.Copilot;

/// <summary>
///     AI tools that expose qyl observability queries to the Copilot agent.
///     Each tool wraps a DuckDbStore query method and formats results for LLM consumption.
/// </summary>
internal static class ObservabilityTools
{
    /// <summary>
    ///     Creates all observability tools backed by the given store.
    /// </summary>
    public static IReadOnlyList<AITool> Create(DuckDbStore store, TimeProvider timeProvider)
    {
        return
        [
            AIFunctionFactory.Create(
                [Description("Search spans by service name, status code, and time range. Returns matching spans with timing and GenAI metadata.")]
                async (string? serviceName, byte? statusCode, int hours, int limit, CancellationToken ct) =>
                {
                    using var activity = CopilotInstrumentation.StartToolSpan("search_spans");
                    CopilotMetrics.RecordToolCall("search_spans", CopilotInstrumentation.GenAiSystem);
                    var startTime = timeProvider.GetUtcNow();

                    var cutoff = timeProvider.GetUtcNow().AddHours(-Math.Clamp(hours, 1, 720));
                    var startAfter = (ulong)cutoff.ToUnixTimeMilliseconds() * 1_000_000UL;

                    var spans = await store.GetSpansAsync(
                        providerName: serviceName,
                        startAfter: startAfter,
                        statusCode: statusCode,
                        limit: Math.Clamp(limit, 1, 200),
                        ct: ct).ConfigureAwait(false);

                    var duration = (timeProvider.GetUtcNow() - startTime).TotalSeconds;
                    CopilotMetrics.RecordToolDuration(duration, "search_spans", CopilotInstrumentation.GenAiSystem);

                    if (spans.Count is 0)
                        return "No spans found matching the criteria.";

                    var sb = new StringBuilder();
                    sb.AppendLine($"Found {spans.Count} spans:");
                    foreach (var span in spans)
                    {
                        sb.AppendLine($"- [{span.Name}] trace={span.TraceId} status={span.StatusCode} service={span.ServiceName ?? "unknown"} duration={span.DurationNs / 1_000_000.0:F1}ms");
                        if (span.GenAiRequestModel is not null)
                            sb.AppendLine($"  model={span.GenAiRequestModel} in={span.GenAiInputTokens} out={span.GenAiOutputTokens}");
                        if (span.StatusMessage is not null)
                            sb.AppendLine($"  message={span.StatusMessage}");
                    }

                    return sb.ToString();
                },
                name: "search_spans",
                description: "Search spans by service name, status code, and time range"),

            AIFunctionFactory.Create(
                [Description("Get all spans belonging to a specific trace by trace ID.")]
                async (string traceId, CancellationToken ct) =>
                {
                    using var activity = CopilotInstrumentation.StartToolSpan("get_trace");
                    CopilotMetrics.RecordToolCall("get_trace", CopilotInstrumentation.GenAiSystem);
                    var startTime = timeProvider.GetUtcNow();

                    var spans = await store.GetTraceAsync(traceId, ct).ConfigureAwait(false);

                    var duration = (timeProvider.GetUtcNow() - startTime).TotalSeconds;
                    CopilotMetrics.RecordToolDuration(duration, "get_trace", CopilotInstrumentation.GenAiSystem);

                    if (spans.Count is 0)
                        return $"No spans found for trace {traceId}.";

                    var sb = new StringBuilder();
                    sb.AppendLine($"Trace {traceId} ({spans.Count} spans):");
                    foreach (var span in spans)
                    {
                        var indent = span.ParentSpanId is not null ? "  " : "";
                        sb.AppendLine($"{indent}- [{span.Name}] span={span.SpanId} parent={span.ParentSpanId ?? "root"} status={span.StatusCode} duration={span.DurationNs / 1_000_000.0:F1}ms");
                        if (span.GenAiRequestModel is not null)
                            sb.AppendLine($"{indent}  model={span.GenAiRequestModel} in={span.GenAiInputTokens} out={span.GenAiOutputTokens}");
                    }

                    return sb.ToString();
                },
                name: "get_trace",
                description: "Get all spans for a trace by trace ID"),

            AIFunctionFactory.Create(
                [Description("Get GenAI usage statistics including request counts, token usage, and costs over a time range. Optionally filter by session.")]
                async (int hours, string? sessionId, CancellationToken ct) =>
                {
                    using var activity = CopilotInstrumentation.StartToolSpan("get_genai_stats");
                    CopilotMetrics.RecordToolCall("get_genai_stats", CopilotInstrumentation.GenAiSystem);
                    var startTime = timeProvider.GetUtcNow();

                    var cutoff = timeProvider.GetUtcNow().AddHours(-Math.Clamp(hours, 1, 720));
                    var startAfter = (ulong)cutoff.ToUnixTimeMilliseconds() * 1_000_000UL;

                    var stats = await store.GetGenAiStatsAsync(sessionId, startAfter, ct).ConfigureAwait(false);

                    var duration = (timeProvider.GetUtcNow() - startTime).TotalSeconds;
                    CopilotMetrics.RecordToolDuration(duration, "get_genai_stats", CopilotInstrumentation.GenAiSystem);

                    return $"""
                        GenAI Statistics (last {hours}h):
                        - Requests: {stats.RequestCount}
                        - Input tokens: {stats.TotalInputTokens:N0}
                        - Output tokens: {stats.TotalOutputTokens:N0}
                        - Total cost: ${stats.TotalCostUsd:F4}
                        - Avg cost/request: ${(stats.RequestCount > 0 ? stats.TotalCostUsd / stats.RequestCount : 0):F4}
                        """;
                },
                name: "get_genai_stats",
                description: "Get GenAI usage statistics (requests, tokens, costs)"),

            AIFunctionFactory.Create(
                [Description("Search logs by severity level, body text, and time range.")]
                async (string? severityLevel, string? body, int hours, int limit, CancellationToken ct) =>
                {
                    using var activity = CopilotInstrumentation.StartToolSpan("search_logs");
                    CopilotMetrics.RecordToolCall("search_logs", CopilotInstrumentation.GenAiSystem);
                    var startTime = timeProvider.GetUtcNow();

                    var cutoff = timeProvider.GetUtcNow().AddHours(-Math.Clamp(hours, 1, 720));
                    var after = (ulong)cutoff.ToUnixTimeMilliseconds() * 1_000_000UL;

                    var logs = await store.GetLogsAsync(
                        severityText: severityLevel,
                        search: body,
                        after: after,
                        limit: Math.Clamp(limit, 1, 200),
                        ct: ct).ConfigureAwait(false);

                    var duration = (timeProvider.GetUtcNow() - startTime).TotalSeconds;
                    CopilotMetrics.RecordToolDuration(duration, "search_logs", CopilotInstrumentation.GenAiSystem);

                    if (logs.Count is 0)
                        return "No logs found matching the criteria.";

                    var sb = new StringBuilder();
                    sb.AppendLine($"Found {logs.Count} logs:");
                    foreach (var log in logs)
                    {
                        sb.AppendLine($"- [{log.SeverityText ?? "UNSET"}] {log.Body ?? "(empty)"} service={log.ServiceName ?? "unknown"}");
                    }

                    return sb.ToString();
                },
                name: "search_logs",
                description: "Search logs by severity and body text"),

            AIFunctionFactory.Create(
                [Description("Get storage statistics including span count, log count, session count, and time range of stored data.")]
                async (CancellationToken ct) =>
                {
                    using var activity = CopilotInstrumentation.StartToolSpan("get_storage_stats");
                    CopilotMetrics.RecordToolCall("get_storage_stats", CopilotInstrumentation.GenAiSystem);
                    var startTime = timeProvider.GetUtcNow();

                    var stats = await store.GetStorageStatsAsync(ct).ConfigureAwait(false);

                    var duration = (timeProvider.GetUtcNow() - startTime).TotalSeconds;
                    CopilotMetrics.RecordToolDuration(duration, "get_storage_stats", CopilotInstrumentation.GenAiSystem);

                    return $"""
                        Storage Statistics:
                        - Spans: {stats.SpanCount:N0}
                        - Logs: {stats.LogCount:N0}
                        - Sessions: {stats.SessionCount:N0}
                        - Storage size: {store.GetStorageSizeBytes() / 1024.0 / 1024.0:F1} MB
                        """;
                },
                name: "get_storage_stats",
                description: "Get storage statistics (counts, size)"),

            AIFunctionFactory.Create(
                [Description("List all spans belonging to a specific session ID, ordered by time.")]
                async (string sessionId, CancellationToken ct) =>
                {
                    using var activity = CopilotInstrumentation.StartToolSpan("list_sessions");
                    CopilotMetrics.RecordToolCall("list_sessions", CopilotInstrumentation.GenAiSystem);
                    var startTime = timeProvider.GetUtcNow();

                    var spans = await store.GetSpansBySessionAsync(sessionId, ct).ConfigureAwait(false);

                    var duration = (timeProvider.GetUtcNow() - startTime).TotalSeconds;
                    CopilotMetrics.RecordToolDuration(duration, "list_sessions", CopilotInstrumentation.GenAiSystem);

                    if (spans.Count is 0)
                        return $"No spans found for session {sessionId}.";

                    var sb = new StringBuilder();
                    sb.AppendLine($"Session {sessionId} ({spans.Count} spans):");
                    foreach (var span in spans)
                    {
                        sb.AppendLine($"- [{span.Name}] trace={span.TraceId} status={span.StatusCode} duration={span.DurationNs / 1_000_000.0:F1}ms");
                        if (span.GenAiRequestModel is not null)
                            sb.AppendLine($"  model={span.GenAiRequestModel} in={span.GenAiInputTokens} out={span.GenAiOutputTokens}");
                    }

                    return sb.ToString();
                },
                name: "list_sessions",
                description: "List spans for a session by session ID"),

            AIFunctionFactory.Create(
                [Description("Get pre-computed system context: topology, performance profile, and alerts. Returns materialized insights that are refreshed every 5 minutes with zero query cost.")]
                async (CancellationToken ct) =>
                {
                    using var activity = CopilotInstrumentation.StartToolSpan("get_system_context");
                    CopilotMetrics.RecordToolCall("get_system_context", CopilotInstrumentation.GenAiSystem);
                    var startTime = timeProvider.GetUtcNow();

                    var rows = await store.GetAllInsightsAsync(ct).ConfigureAwait(false);

                    var duration = (timeProvider.GetUtcNow() - startTime).TotalSeconds;
                    CopilotMetrics.RecordToolDuration(duration, "get_system_context", CopilotInstrumentation.GenAiSystem);

                    if (rows.Count is 0)
                        return "No insights materialized yet. The system context is generated every 5 minutes after ingestion starts.";

                    var sb = new StringBuilder();
                    sb.AppendLine("# System Context (auto-generated from telemetry)");
                    sb.AppendLine();
                    foreach (var row in rows)
                    {
                        sb.AppendLine(row.ContentMarkdown);
                        sb.AppendLine();
                    }

                    return sb.ToString();
                },
                name: "get_system_context",
                description: "Get pre-computed system context (topology, performance, alerts)")
        ];
    }
}
