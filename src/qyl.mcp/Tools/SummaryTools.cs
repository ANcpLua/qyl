using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;
using qyl.mcp.Agents;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools that fetch observability data via HTTP then feed it to an LLM
///     for structured summarization. Combines the HTTP-delegating pattern from
///     <see cref="GenAiTools"/> with the IChatClient pattern from <see cref="UseQylTools"/>.
/// </summary>
[McpServerToolType]
internal sealed class SummaryTools(HttpClient client, IConfiguration config)
{
    private readonly IChatClient? _llm = AgentLlmFactory.TryCreate(config);

    [McpServerTool(Name = "qyl.summarize_error", Title = "Summarize Error",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("""
                 Generate an AI-powered summary of an error issue.

                 Fetches error details and recent events, then uses an LLM
                 to produce a structured analysis including:
                 - What's wrong and possible causes
                 - Impact assessment
                 - Fixability score (1-5)
                 - Suggested fix

                 Requires QYL_AGENT_API_KEY to be configured.
                 Falls back to raw data display if no LLM available.

                 Returns: AI-generated error analysis
                 """)]
    public Task<string> SummarizeErrorAsync(
        [Description("The error issue ID to summarize")] string issueId,
        CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            // Fetch error data
            SummaryIssueDto? issue = await client.GetFromJsonAsync<SummaryIssueDto>(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}",
                SummaryJsonContext.Default.SummaryIssueDto, ct).ConfigureAwait(false);

            if (issue is null)
                return $"Error issue '{issueId}' not found.";

            SummaryEventsResponse? eventsResponse = await client.GetFromJsonAsync<SummaryEventsResponse>(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/events?limit=5",
                SummaryJsonContext.Default.SummaryEventsResponse, ct).ConfigureAwait(false);

            // Build data context for LLM
            StringBuilder sb = new();
            sb.AppendLine($"Error Issue: {issue.Title}");
            sb.AppendLine($"Type: {issue.ErrorType}, Category: {issue.Category}");
            sb.AppendLine($"Status: {issue.Status}, Priority: {issue.Priority}");
            sb.AppendLine($"Occurrences: {issue.OccurrenceCount}, Affected Users: {issue.AffectedUsersCount}");
            sb.AppendLine($"First Seen: {issue.FirstSeenAt:u}, Last Seen: {issue.LastSeenAt:u}");
            if (issue.Culprit is not null) sb.AppendLine($"Culprit: {issue.Culprit}");

            if (eventsResponse?.Items is { Count: > 0 })
            {
                sb.AppendLine("\nRecent Events:");
                foreach (SummaryEventDto evt in eventsResponse.Items)
                {
                    sb.AppendLine($"- [{evt.Timestamp:u}] {evt.Message}");
                    if (evt.StackTrace is not null)
                        sb.AppendLine($"  Stack: {Truncate(evt.StackTrace, 500)}");
                }
            }

            if (_llm is null)
                return $"# Error Summary (raw data -- no LLM configured)\n\n{sb}";

            List<ChatMessage> messages =
            [
                new(ChatRole.System, ErrorSummaryPrompt.Prompt),
                new(ChatRole.User, sb.ToString())
            ];

            ChatResponse response = await _llm.GetResponseAsync(messages, cancellationToken: ct).ConfigureAwait(false);
            return response.Text ?? "Summary generation produced no output.";
        });

    [McpServerTool(Name = "qyl.summarize_trace", Title = "Summarize Trace",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("""
                 Generate an AI-powered summary of a distributed trace.

                 Fetches all spans for the trace, then uses an LLM to produce
                 a structured analysis including:
                 - Overview of the request flow
                 - Performance bottlenecks and critical path
                 - Error analysis
                 - GenAI cost/latency breakdown (if applicable)

                 Requires QYL_AGENT_API_KEY to be configured.
                 Falls back to raw data display if no LLM available.

                 Returns: AI-generated trace analysis
                 """)]
    public Task<string> SummarizeTraceAsync(
        [Description("The trace ID to summarize")] string traceId,
        CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            // Fetch trace data (list of spans)
            List<TraceSpanDto>? spans = await client.GetFromJsonAsync<List<TraceSpanDto>>(
                $"/api/v1/traces/{Uri.EscapeDataString(traceId)}",
                SummaryJsonContext.Default.ListTraceSpanDto, ct).ConfigureAwait(false);

            if (spans is null || spans.Count is 0)
                return $"Trace '{traceId}' not found or contains no spans.";

            // Build data context for LLM
            StringBuilder sb = new();
            sb.AppendLine($"Trace ID: {traceId}");
            sb.AppendLine($"Total Spans: {spans.Count}");

            long minStart = spans.Min(static s => s.StartTimeUnixNano);
            long maxEnd = spans.Max(static s => s.StartTimeUnixNano + s.DurationNs);
            double totalDurationMs = (maxEnd - minStart) / 1_000_000.0;
            sb.AppendLine($"Total Duration: {totalDurationMs:F1}ms");

            int errorCount = spans.Count(static s => s.StatusCode == 2);
            if (errorCount > 0)
                sb.AppendLine($"Error Spans: {errorCount}");

            sb.AppendLine("\nSpans:");
            foreach (TraceSpanDto span in spans.OrderBy(static s => s.StartTimeUnixNano))
            {
                double durationMs = span.DurationNs / 1_000_000.0;
                string statusLabel = span.StatusCode == 2 ? " [ERROR]" : "";
                string parentInfo = span.ParentSpanId is not null ? $" (parent: {span.ParentSpanId})" : " (root)";

                sb.AppendLine($"- {span.Name}{statusLabel} — {durationMs:F1}ms");
                sb.AppendLine($"  Service: {span.ServiceName ?? "unknown"}, SpanID: {span.SpanId}{parentInfo}");

                if (span.StatusCode == 2 && span.StatusMessage is not null)
                    sb.AppendLine($"  Error: {span.StatusMessage}");
            }

            if (_llm is null)
                return $"# Trace Summary (raw data -- no LLM configured)\n\n{sb}";

            List<ChatMessage> messages =
            [
                new(ChatRole.System, TraceSummaryPrompt.Prompt),
                new(ChatRole.User, sb.ToString())
            ];

            ChatResponse response = await _llm.GetResponseAsync(messages, cancellationToken: ct).ConfigureAwait(false);
            return response.Text ?? "Summary generation produced no output.";
        });

    [McpServerTool(Name = "qyl.summarize_session", Title = "Summarize Session",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("""
                 Generate an AI-powered summary of a session.

                 Fetches session metadata and its spans, then uses an LLM
                 to produce a structured analysis including:
                 - Session overview and purpose
                 - Span timeline and interactions
                 - Error analysis
                 - Performance characteristics

                 Requires QYL_AGENT_API_KEY to be configured.
                 Falls back to raw data display if no LLM available.

                 Returns: AI-generated session analysis
                 """)]
    public Task<string> SummarizeSessionAsync(
        [Description("The session ID to summarize")] string sessionId,
        CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            // Fetch session metadata and spans concurrently
            Task<SessionDto?> sessionTask = client.GetFromJsonAsync<SessionDto>(
                $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}",
                SummaryJsonContext.Default.SessionDto, ct);

            Task<SessionSpansResponse?> spansTask = client.GetFromJsonAsync<SessionSpansResponse>(
                $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}/spans?limit=50",
                SummaryJsonContext.Default.SessionSpansResponse, ct);

            SessionDto? session = await sessionTask.ConfigureAwait(false);
            SessionSpansResponse? spansResponse = await spansTask.ConfigureAwait(false);

            if (session is null)
                return $"Session '{sessionId}' not found.";

            // Build data context for LLM
            StringBuilder sb = new();
            sb.AppendLine($"Session ID: {session.SessionId}");
            if (session.ServiceName is not null)
                sb.AppendLine($"Service: {session.ServiceName}");
            if (session.StartTime.HasValue)
                sb.AppendLine($"Start: {session.StartTime.Value:u}");
            if (session.EndTime.HasValue)
                sb.AppendLine($"End: {session.EndTime.Value:u}");
            sb.AppendLine($"Span Count: {session.SpanCount}");
            sb.AppendLine($"Error Count: {session.ErrorCount}");

            if (spansResponse?.Items is { Count: > 0 })
            {
                sb.AppendLine($"\nSpans ({spansResponse.Items.Count} shown):");
                foreach (SessionSpanDto span in spansResponse.Items)
                {
                    double durationMs = span.DurationNs / 1_000_000.0;
                    string statusLabel = span.StatusCode == 2 ? " [ERROR]" : "";

                    sb.AppendLine($"- {span.Name}{statusLabel} — {durationMs:F1}ms");
                    if (span.ServiceName is not null)
                        sb.AppendLine($"  Service: {span.ServiceName}");
                    if (span.StatusCode == 2 && span.StatusMessage is not null)
                        sb.AppendLine($"  Error: {span.StatusMessage}");
                }
            }

            if (_llm is null)
                return $"# Session Summary (raw data -- no LLM configured)\n\n{sb}";

            List<ChatMessage> messages =
            [
                new(ChatRole.System, TraceSummaryPrompt.Prompt),
                new(ChatRole.User, sb.ToString())
            ];

            ChatResponse response = await _llm.GetResponseAsync(messages, cancellationToken: ct).ConfigureAwait(false);
            return response.Text ?? "Summary generation produced no output.";
        });

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}

#region DTOs

internal sealed record SummaryIssueDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("error_type")] string ErrorType,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("occurrence_count")] long OccurrenceCount,
    [property: JsonPropertyName("affected_users_count")] int AffectedUsersCount,
    [property: JsonPropertyName("first_seen_at")] DateTime FirstSeenAt,
    [property: JsonPropertyName("last_seen_at")] DateTime LastSeenAt,
    [property: JsonPropertyName("culprit")] string? Culprit);

internal sealed record SummaryEventDto(
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("stack_trace")] string? StackTrace);

internal sealed record SummaryEventsResponse(
    [property: JsonPropertyName("items")] List<SummaryEventDto>? Items);

internal sealed record TraceSpanDto(
    [property: JsonPropertyName("trace_id")] string TraceId,
    [property: JsonPropertyName("span_id")] string SpanId,
    [property: JsonPropertyName("parent_span_id")] string? ParentSpanId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("service_name")] string? ServiceName,
    [property: JsonPropertyName("start_time_unix_nano")] long StartTimeUnixNano,
    [property: JsonPropertyName("duration_ns")] long DurationNs,
    [property: JsonPropertyName("status_code")] int StatusCode,
    [property: JsonPropertyName("status_message")] string? StatusMessage);

internal sealed record SessionDto(
    [property: JsonPropertyName("session_id")] string SessionId,
    [property: JsonPropertyName("service_name")] string? ServiceName,
    [property: JsonPropertyName("start_time")] DateTime? StartTime,
    [property: JsonPropertyName("end_time")] DateTime? EndTime,
    [property: JsonPropertyName("span_count")] int SpanCount,
    [property: JsonPropertyName("error_count")] int ErrorCount);

internal sealed record SessionSpanDto(
    [property: JsonPropertyName("span_id")] string SpanId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("service_name")] string? ServiceName,
    [property: JsonPropertyName("start_time_unix_nano")] long StartTimeUnixNano,
    [property: JsonPropertyName("duration_ns")] long DurationNs,
    [property: JsonPropertyName("status_code")] int StatusCode,
    [property: JsonPropertyName("status_message")] string? StatusMessage);

internal sealed record SessionSpansResponse(
    [property: JsonPropertyName("items")] List<SessionSpanDto>? Items);

#endregion

[JsonSerializable(typeof(SummaryIssueDto))]
[JsonSerializable(typeof(SummaryEventsResponse))]
[JsonSerializable(typeof(List<TraceSpanDto>))]
[JsonSerializable(typeof(SessionDto))]
[JsonSerializable(typeof(SessionSpansResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class SummaryJsonContext : JsonSerializerContext;
