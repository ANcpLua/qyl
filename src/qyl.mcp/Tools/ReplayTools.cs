using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for replaying and analyzing stored AI sessions.
///     Fetches data from qyl.collector via HTTP.
/// </summary>
[McpServerToolType]
public sealed class ReplayTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_sessions")]
    [Description("""
                 List AI sessions captured by qyl for replay or analysis.

                 A session groups related AI interactions (prompts, completions, tool calls).
                 Use this to find sessions to investigate, then use get_session_transcript
                 or analyze_session_errors for details.

                 Example queries:
                 - Recent sessions: list_sessions()
                 - From specific service: list_sessions(service_name="my-api")
                 - More results: list_sessions(limit=50)

                 Returns: Session IDs with span counts, error counts, token usage, and costs
                 """)]
    public async Task<string> ListSessionsAsync(
        [Description("Maximum sessions to return (default: 20, max: 100)")]
        int limit = 20,
        [Description("Filter by service/application name")]
        string? serviceName = null)
    {
        try
        {
            var url = $"/api/v1/sessions?limit={limit}";
            if (!string.IsNullOrEmpty(serviceName))
                url += $"&serviceName={Uri.EscapeDataString(serviceName)}";

            var response = await client.GetFromJsonAsync<SessionListResponse>(
                url, ReplayJsonContext.Default.SessionListResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count is 0)
                return "No sessions found";

            var sb = new StringBuilder();
            sb.AppendLine($"Found {response.Items.Count} sessions:");
            sb.AppendLine();

            foreach (var session in response.Items)
            {
                sb.AppendLine($"**Session: {session.SessionId}**");
                sb.AppendLine($"  - Service: {session.ServiceName ?? "unknown"}");
                sb.AppendLine($"  - Spans: {session.SpanCount}");
                sb.AppendLine($"  - Errors: {session.ErrorCount}");
                if (session.TotalInputTokens > 0 || session.TotalOutputTokens > 0)
                {
                    sb.AppendLine($"  - Tokens: {session.TotalInputTokens} in / {session.TotalOutputTokens} out");
                    if (session.TotalCostUsd > 0)
                        sb.AppendLine($"  - Cost: ${session.TotalCostUsd:F4}");
                }

                sb.AppendLine($"  - Started: {session.StartTime:u}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching sessions: {ex.Message}";
        }
    }

    [McpServerTool(Name = "qyl.get_session_transcript")]
    [Description("""
                 Get a human-readable transcript of an AI session.

                 Shows the timeline of AI operations including:
                 - Provider and model used for each call
                 - Input/output token counts
                 - Cost estimates in USD
                 - Duration of each operation
                 - Errors and their messages

                 Use list_sessions first to find a session_id.

                 Example: get_session_transcript(session_id="session-abc123")

                 Returns: Formatted transcript with timing, tokens, costs, and errors
                 """)]
    public async Task<string> GetSessionTranscriptAsync(
        [Description("The session ID from list_sessions (required)")]
        string sessionId)
    {
        try
        {
            var response = await client.GetFromJsonAsync<SpanListResponse>(
                $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}/spans",
                ReplayJsonContext.Default.SpanListResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count is 0)
                return $"Session '{sessionId}' not found or has no spans";

            var sb = new StringBuilder();
            sb.AppendLine($"# Session Transcript: {sessionId}");
            sb.AppendLine($"Total Spans: {response.Items.Count}");
            sb.AppendLine();

            // Sort by start time
            var sortedSpans = response.Items.OrderBy(static s => s.StartTimeUnixNano).ToList();

            foreach (var span in sortedSpans)
            {
                var durationMs = span.DurationNs / 1_000_000.0;
                var isGenAi = !string.IsNullOrEmpty(span.GenAiProviderName);

                sb.AppendLine($"## {span.Name}");
                sb.AppendLine($"- Duration: {durationMs:F1}ms");

                if (isGenAi)
                {
                    sb.AppendLine($"- Provider: {span.GenAiProviderName}");
                    sb.AppendLine($"- Model: {span.GenAiRequestModel}");
                    if (span.GenAiInputTokens > 0 || span.GenAiOutputTokens > 0)
                        sb.AppendLine($"- Tokens: {span.GenAiInputTokens} in / {span.GenAiOutputTokens} out");
                    if (span.GenAiCostUsd > 0)
                        sb.AppendLine($"- Cost: ${span.GenAiCostUsd:F6}");
                }

                if (span.StatusCode == 2) // ERROR
                    sb.AppendLine($"- **ERROR**: {span.StatusMessage ?? "Unknown error"}");

                sb.AppendLine();
            }

            // Summary
            var totalTokensIn = sortedSpans.Sum(static s => s.GenAiInputTokens ?? 0L);
            var totalTokensOut = sortedSpans.Sum(static s => s.GenAiOutputTokens ?? 0L);
            var totalCost = sortedSpans.Sum(static s => s.GenAiCostUsd ?? 0d);
            var totalDurationMs = sortedSpans.Sum(static s => s.DurationNs) / 1_000_000.0;

            sb.AppendLine("---");
            sb.AppendLine("## Summary");
            sb.AppendLine($"- Total Duration: {totalDurationMs:F1}ms");
            if (totalTokensIn > 0 || totalTokensOut > 0)
                sb.AppendLine($"- Total Tokens: {totalTokensIn} in / {totalTokensOut} out");
            if (totalCost > 0)
                sb.AppendLine($"- Total Cost: ${totalCost:F4}");

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching session: {ex.Message}";
        }
    }

    [McpServerTool(Name = "qyl.get_trace")]
    [Description("""
                 Get the complete span tree for a distributed trace.

                 A trace shows all operations across services for a single request.
                 Each span represents one operation (AI call, HTTP request, DB query).

                 The trace_id can be found in:
                 - Session transcript spans
                 - Error reports
                 - Structured logs

                 Example: get_trace(trace_id="abc123def456...")

                 Returns: Span hierarchy with timing, status, and GenAI attributes
                 """)]
    public async Task<string> GetTraceAsync(
        [Description("The trace ID (hex string, required)")]
        string traceId)
    {
        try
        {
            if (await client.GetFromJsonAsync<TraceResponse>($"/api/v1/traces/{Uri.EscapeDataString(traceId)}",
                    ReplayJsonContext.Default.TraceResponse).ConfigureAwait(false) is not { } response)
                return $"Trace '{traceId}' not found";

            var sb = new StringBuilder();
            sb.AppendLine($"# Trace: {traceId}");
            sb.AppendLine($"Root Span: {response.RootSpan?.Name ?? "unknown"}");
            sb.AppendLine($"Total Duration: {response.DurationMs:F1}ms");
            sb.AppendLine($"Status: {response.Status ?? "unknown"}");
            sb.AppendLine($"Span Count: {response.Spans?.Count ?? 0}");
            sb.AppendLine();

            if (response.Spans is { Count: > 0 })
            {
                sb.AppendLine("## Spans");
                foreach (var span in response.Spans.OrderBy(static s => s.StartTimeUnixNano))
                {
                    var indent = string.IsNullOrEmpty(span.ParentSpanId) ? "" : "  ";
                    var durationMs = span.DurationNs / 1_000_000.0;
                    sb.AppendLine($"{indent}- **{span.Name}** ({durationMs:F1}ms)");

                    if (!string.IsNullOrEmpty(span.GenAiProviderName))
                        sb.AppendLine($"{indent}  Model: {span.GenAiRequestModel}");
                }
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching trace: {ex.Message}";
        }
    }

    [McpServerTool(Name = "qyl.analyze_session_errors")]
    [Description("""
                 Analyze all errors in a session.

                 Shows failed spans with:
                 - Error messages and types
                 - Provider and model that failed
                 - Span timing and context
                 - Parent-child relationships

                 Use this to understand why an AI workflow failed.

                 Example: analyze_session_errors(session_id="session-abc123")

                 Returns: Error details grouped by span with full context
                 """)]
    public async Task<string> AnalyzeSessionErrorsAsync(
        [Description("The session ID from list_sessions (required)")]
        string sessionId)
    {
        try
        {
            var response = await client.GetFromJsonAsync<SpanListResponse>(
                $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}/spans",
                ReplayJsonContext.Default.SpanListResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count is 0)
                return $"Session '{sessionId}' not found or has no spans";

            var errorSpans = response.Items.Where(static s => s.StatusCode == 2).ToList();

            if (errorSpans.Count is 0)
                return $"No errors found in session '{sessionId}'";

            var sb = new StringBuilder();
            sb.AppendLine($"# Errors in Session: {sessionId}");
            sb.AppendLine($"Found {errorSpans.Count} error(s)");
            sb.AppendLine();

            foreach (var span in errorSpans)
            {
                var durationMs = span.DurationNs / 1_000_000.0;

                sb.AppendLine($"## Error: {span.Name}");
                sb.AppendLine($"- Span ID: {span.SpanId}");
                sb.AppendLine($"- Duration: {durationMs:F1}ms");
                sb.AppendLine($"- Message: {span.StatusMessage ?? "No message"}");

                if (!string.IsNullOrEmpty(span.GenAiProviderName))
                {
                    sb.AppendLine($"- Provider: {span.GenAiProviderName}");
                    sb.AppendLine($"- Model: {span.GenAiRequestModel}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error analyzing session: {ex.Message}";
        }
    }
}

#region Response Types

internal sealed record SessionListResponse(
    [property: JsonPropertyName("items")] List<SessionSummary>? Items,
    [property: JsonPropertyName("total")] int Total);

internal sealed record SessionSummary(
    [property: JsonPropertyName("session_id")]
    string SessionId,
    [property: JsonPropertyName("service_name")]
    string? ServiceName,
    [property: JsonPropertyName("span_count")]
    int SpanCount,
    [property: JsonPropertyName("error_count")]
    int ErrorCount,
    [property: JsonPropertyName("total_input_tokens")]
    long TotalInputTokens,
    [property: JsonPropertyName("total_output_tokens")]
    long TotalOutputTokens,
    [property: JsonPropertyName("total_cost_usd")]
    double TotalCostUsd,
    [property: JsonPropertyName("start_time")]
    string StartTime);

internal sealed record SpanListResponse(
    [property: JsonPropertyName("items")] List<SpanDto>? Items);

internal sealed record SpanDto(
    [property: JsonPropertyName("trace_id")]
    string TraceId,
    [property: JsonPropertyName("span_id")]
    string SpanId,
    [property: JsonPropertyName("parent_span_id")]
    string? ParentSpanId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("start_time_unix_nano")]
    long StartTimeUnixNano,
    [property: JsonPropertyName("duration_ns")]
    long DurationNs,
    [property: JsonPropertyName("status_code")]
    int StatusCode,
    [property: JsonPropertyName("status_message")]
    string? StatusMessage,
    [property: JsonPropertyName("gen_ai_provider_name")]
    string? GenAiProviderName,
    [property: JsonPropertyName("gen_ai_request_model")]
    string? GenAiRequestModel,
    [property: JsonPropertyName("gen_ai_input_tokens")]
    long? GenAiInputTokens,
    [property: JsonPropertyName("gen_ai_output_tokens")]
    long? GenAiOutputTokens,
    [property: JsonPropertyName("gen_ai_cost_usd")]
    double? GenAiCostUsd);

internal sealed record TraceResponse(
    [property: JsonPropertyName("trace_id")]
    string TraceId,
    [property: JsonPropertyName("spans")] List<SpanDto>? Spans,
    [property: JsonPropertyName("root_span")]
    SpanDto? RootSpan,
    [property: JsonPropertyName("duration_ms")]
    double? DurationMs,
    [property: JsonPropertyName("status")] string? Status);

#endregion

[JsonSerializable(typeof(SessionListResponse))]
[JsonSerializable(typeof(SpanListResponse))]
[JsonSerializable(typeof(TraceResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class ReplayJsonContext : JsonSerializerContext;
