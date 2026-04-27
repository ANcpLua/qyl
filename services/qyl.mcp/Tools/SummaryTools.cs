using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Agents;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools that fetch observability data via HTTP, then feed it to a
///     <c>ChatClientAgent</c> for structured summarization. Agents are sourced
///     from <see cref="IQylMcpAgentsBuilder" /> so the
///     <c>.AsBuilder().UseQylAgentTelemetry().Build()</c> wrap is centralized and
///     <c>QYL0135</c> is satisfied at the construction site. No tools are attached
///     — these are pure LLM-as-summarizer calls, so the default
///     <c>FunctionInvokingChatClient</c> inserted by <c>AsAIAgent</c> is a no-op
///     passthrough.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Agent)]
internal sealed class SummaryTools(HttpClient client, IQylMcpAgentsBuilder agents)
{

    [QylCapability("agentic_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.summarize_error", Title = "Summarize Error",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
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
        [Description("The error issue ID to summarize")]
        string issueId,
        CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            // Fetch error data
            var issue = await client.GetFromJsonAsync<SummaryIssueDto>(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}",
                SummaryJsonContext.Default.SummaryIssueDto, ct).ConfigureAwait(false);

            if (issue is null)
                return $"Error issue '{issueId}' not found.";

            var eventsResponse = await client.GetFromJsonAsync<SummaryEventsResponse>(
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
                foreach (var evt in eventsResponse.Items)
                {
                    sb.AppendLine($"- [{evt.Timestamp:u}] {evt.Message}");
                    if (evt.StackTrace is not null)
                        sb.AppendLine($"  Stack: {Truncate(evt.StackTrace, 500)}");
                }
            }

            if (!agents.IsConfigured)
                return $"# Error Summary (raw data -- no LLM configured)\n\n{sb}";

            var agent = agents.BuildSummarizeErrorAgent();

            var response = await agent.RunAsync(sb.ToString(), cancellationToken: ct).ConfigureAwait(false);
            return response.Text is { Length: > 0 } text ? text : "Summary generation produced no output.";
        });

    [QylCapability("agentic_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.summarize_trace", Title = "Summarize Trace",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
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
        [Description("The trace ID to summarize")]
        string traceId,
        CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            // Fetch trace data (list of spans)
            var spans = await client.GetFromJsonAsync<List<TraceSpanDto>>(
                $"/api/v1/traces/{Uri.EscapeDataString(traceId)}",
                SummaryJsonContext.Default.ListTraceSpanDto, ct).ConfigureAwait(false);

            if (spans is null || spans.Count is 0)
                return $"Trace '{traceId}' not found or contains no spans.";

            // Build data context for LLM
            StringBuilder sb = new();
            sb.AppendLine($"Trace ID: {traceId}");
            sb.AppendLine($"Total Spans: {spans.Count}");

            var minStart = spans.Min(static s => s.StartTimeUnixNano);
            var maxEnd = spans.Max(static s => s.StartTimeUnixNano + s.DurationNs);
            var totalDurationMs = (maxEnd - minStart) / 1_000_000.0;
            sb.AppendLine($"Total Duration: {totalDurationMs:F1}ms");

            var errorCount = spans.Count(static s => s.StatusCode == 2);
            if (errorCount > 0)
                sb.AppendLine($"Error Spans: {errorCount}");

            sb.AppendLine("\nSpans:");
            foreach (var span in spans.OrderBy(static s => s.StartTimeUnixNano))
            {
                var durationMs = span.DurationNs / 1_000_000.0;
                var statusLabel = span.StatusCode == 2 ? " [ERROR]" : "";
                var parentInfo = span.ParentSpanId is not null ? $" (parent: {span.ParentSpanId})" : " (root)";

                sb.AppendLine($"- {span.Name}{statusLabel} — {durationMs:F1}ms");
                sb.AppendLine($"  Service: {span.ServiceName ?? "unknown"}, SpanID: {span.SpanId}{parentInfo}");

                if (span is { StatusCode: 2, StatusMessage: not null })
                    sb.AppendLine($"  Error: {span.StatusMessage}");
            }

            if (!agents.IsConfigured)
                return $"# Trace Summary (raw data -- no LLM configured)\n\n{sb}";

            var agent = agents.BuildSummarizeTraceAgent();

            var response = await agent.RunAsync(sb.ToString(), cancellationToken: ct).ConfigureAwait(false);
            return response.Text is { Length: > 0 } text ? text : "Summary generation produced no output.";
        });

    [McpServerTool(Name = "qyl.summarize_session", Title = "Summarize Session",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
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
        [Description("The session ID to summarize")]
        string sessionId,
        CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            // Fetch session metadata and spans concurrently
            var sessionTask = client.GetFromJsonAsync<SessionDto>(
                $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}",
                SummaryJsonContext.Default.SessionDto, ct);

            var spansTask = client.GetFromJsonAsync<SessionSpansResponse>(
                $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}/spans?limit=50",
                SummaryJsonContext.Default.SessionSpansResponse, ct);

            var session = await sessionTask.ConfigureAwait(false);
            var spansResponse = await spansTask.ConfigureAwait(false);

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
                foreach (var span in spansResponse.Items)
                {
                    var durationMs = span.DurationNs / 1_000_000.0;
                    var statusLabel = span.StatusCode == 2 ? " [ERROR]" : "";

                    sb.AppendLine($"- {span.Name}{statusLabel} — {durationMs:F1}ms");
                    if (span.ServiceName is not null)
                        sb.AppendLine($"  Service: {span.ServiceName}");
                    if (span is { StatusCode: 2, StatusMessage: not null })
                        sb.AppendLine($"  Error: {span.StatusMessage}");
                }
            }

            if (!agents.IsConfigured)
                return $"# Session Summary (raw data -- no LLM configured)\n\n{sb}";

            var agent = agents.BuildSummarizeSessionAgent();

            var response = await agent.RunAsync(sb.ToString(), cancellationToken: ct).ConfigureAwait(false);
            return response.Text is { Length: > 0 } text ? text : "Summary generation produced no output.";
        });

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}

#region DTOs

internal sealed record SummaryIssueDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("error_type")]
    string ErrorType,
    [property: JsonPropertyName("category")]
    string Category,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")]
    string Priority,
    [property: JsonPropertyName("occurrence_count")]
    long OccurrenceCount,
    [property: JsonPropertyName("affected_users_count")]
    int AffectedUsersCount,
    [property: JsonPropertyName("first_seen_at")]
    DateTime FirstSeenAt,
    [property: JsonPropertyName("last_seen_at")]
    DateTime LastSeenAt,
    [property: JsonPropertyName("culprit")]
    string? Culprit);

internal sealed record SummaryEventDto(
    [property: JsonPropertyName("timestamp")]
    DateTime Timestamp,
    [property: JsonPropertyName("message")]
    string? Message,
    [property: JsonPropertyName("stack_trace")]
    string? StackTrace);

internal sealed record SummaryEventsResponse(
    [property: JsonPropertyName("items")] List<SummaryEventDto>? Items);

internal sealed record TraceSpanDto(
    [property: JsonPropertyName("trace_id")]
    string TraceId,
    [property: JsonPropertyName("span_id")]
    string SpanId,
    [property: JsonPropertyName("parent_span_id")]
    string? ParentSpanId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("service_name")]
    string? ServiceName,
    [property: JsonPropertyName("start_time_unix_nano")]
    long StartTimeUnixNano,
    [property: JsonPropertyName("duration_ns")]
    long DurationNs,
    [property: JsonPropertyName("status_code")]
    int StatusCode,
    [property: JsonPropertyName("status_message")]
    string? StatusMessage);

internal sealed record SessionDto(
    [property: JsonPropertyName("session_id")]
    string SessionId,
    [property: JsonPropertyName("service_name")]
    string? ServiceName,
    [property: JsonPropertyName("start_time")]
    DateTime? StartTime,
    [property: JsonPropertyName("end_time")]
    DateTime? EndTime,
    [property: JsonPropertyName("span_count")]
    int SpanCount,
    [property: JsonPropertyName("error_count")]
    int ErrorCount);

internal sealed record SessionSpanDto(
    [property: JsonPropertyName("span_id")]
    string SpanId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("service_name")]
    string? ServiceName,
    [property: JsonPropertyName("start_time_unix_nano")]
    long StartTimeUnixNano,
    [property: JsonPropertyName("duration_ns")]
    long DurationNs,
    [property: JsonPropertyName("status_code")]
    int StatusCode,
    [property: JsonPropertyName("status_message")]
    string? StatusMessage);

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
