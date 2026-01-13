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
public sealed class ReplayTools
{
    private readonly HttpClient _client;

    public ReplayTools(HttpClient client) => _client = client;

    [McpServerTool(Name = "qyl.list_sessions")]
    [Description("""
                 List available sessions for replay or analysis.

                 Parameters:
                 - limit: Max sessions to return (default: 20)
                 - service_name: Filter by service name

                 Returns: List of session IDs with summary info
                 """)]
    public async Task<string> ListSessionsAsync(
        [Description("Maximum number of sessions to return")]
        int limit = 20,
        [Description("Filter by service name")]
        string? service_name = null)
    {
        try
        {
            var url = $"/api/v1/sessions?limit={limit}";
            if (!string.IsNullOrEmpty(service_name))
                url += $"&serviceName={Uri.EscapeDataString(service_name)}";

            var response = await _client.GetFromJsonAsync<SessionListResponse>(
                url, ReplayJsonContext.Default.SessionListResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count == 0)
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
                 Shows prompts, responses, models used, and timing information.

                 Parameters:
                 - session_id: The session ID to analyze

                 Returns: Formatted transcript of the session
                 """)]
    public async Task<string> GetSessionTranscriptAsync(
        [Description("The session ID to get transcript for")]
        string session_id)
    {
        try
        {
            var response = await _client.GetFromJsonAsync<SpanListResponse>(
                $"/api/v1/sessions/{Uri.EscapeDataString(session_id)}/spans",
                ReplayJsonContext.Default.SpanListResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count == 0)
                return $"Session '{session_id}' not found or has no spans";

            var sb = new StringBuilder();
            sb.AppendLine($"# Session Transcript: {session_id}");
            sb.AppendLine($"Total Spans: {response.Items.Count}");
            sb.AppendLine();

            // Sort by start time
            var sortedSpans = response.Items.OrderBy(s => s.StartTimeUnixNano).ToList();

            foreach (var span in sortedSpans)
            {
                var durationMs = span.DurationNs / 1_000_000.0;
                var isGenAi = !string.IsNullOrEmpty(span.GenAiSystem);

                sb.AppendLine($"## {span.Name}");
                sb.AppendLine($"- Duration: {durationMs:F1}ms");

                if (isGenAi)
                {
                    sb.AppendLine($"- Provider: {span.GenAiSystem}");
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
            var totalTokensIn = sortedSpans.Sum(s => s.GenAiInputTokens ?? 0);
            var totalTokensOut = sortedSpans.Sum(s => s.GenAiOutputTokens ?? 0);
            var totalCost = sortedSpans.Sum(s => s.GenAiCostUsd ?? 0);
            var totalDurationMs = sortedSpans.Sum(s => s.DurationNs) / 1_000_000.0;

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
                 Get detailed information about a specific trace.
                 Shows the full span tree with timing and attributes.

                 Parameters:
                 - trace_id: The trace ID to analyze

                 Returns: Trace details with span hierarchy
                 """)]
    public async Task<string> GetTraceAsync(
        [Description("The trace ID to get details for")]
        string trace_id)
    {
        try
        {
            var response = await _client.GetFromJsonAsync<TraceResponse>(
                $"/api/v1/traces/{Uri.EscapeDataString(trace_id)}",
                ReplayJsonContext.Default.TraceResponse).ConfigureAwait(false);

            if (response is null)
                return $"Trace '{trace_id}' not found";

            var sb = new StringBuilder();
            sb.AppendLine($"# Trace: {trace_id}");
            sb.AppendLine($"Root Span: {response.RootSpan?.Name ?? "unknown"}");
            sb.AppendLine($"Total Duration: {response.DurationMs:F1}ms");
            sb.AppendLine($"Status: {response.Status ?? "unknown"}");
            sb.AppendLine($"Span Count: {response.Spans?.Count ?? 0}");
            sb.AppendLine();

            if (response.Spans is { Count: > 0 })
            {
                sb.AppendLine("## Spans");
                foreach (var span in response.Spans.OrderBy(s => s.StartTimeUnixNano))
                {
                    var indent = string.IsNullOrEmpty(span.ParentSpanId) ? "" : "  ";
                    var durationMs = span.DurationNs / 1_000_000.0;
                    sb.AppendLine($"{indent}- **{span.Name}** ({durationMs:F1}ms)");

                    if (!string.IsNullOrEmpty(span.GenAiSystem))
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
                 Analyze errors in a session.
                 Shows all failed spans with error details.

                 Parameters:
                 - session_id: The session ID to analyze

                 Returns: List of errors with context
                 """)]
    public async Task<string> AnalyzeSessionErrorsAsync(
        [Description("The session ID to analyze for errors")]
        string session_id)
    {
        try
        {
            var response = await _client.GetFromJsonAsync<SpanListResponse>(
                $"/api/v1/sessions/{Uri.EscapeDataString(session_id)}/spans",
                ReplayJsonContext.Default.SpanListResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count == 0)
                return $"Session '{session_id}' not found or has no spans";

            var errorSpans = response.Items.Where(s => s.StatusCode == 2).ToList();

            if (errorSpans.Count == 0)
                return $"No errors found in session '{session_id}'";

            var sb = new StringBuilder();
            sb.AppendLine($"# Errors in Session: {session_id}");
            sb.AppendLine($"Found {errorSpans.Count} error(s)");
            sb.AppendLine();

            foreach (var span in errorSpans)
            {
                var durationMs = span.DurationNs / 1_000_000.0;

                sb.AppendLine($"## Error: {span.Name}");
                sb.AppendLine($"- Span ID: {span.SpanId}");
                sb.AppendLine($"- Duration: {durationMs:F1}ms");
                sb.AppendLine($"- Message: {span.StatusMessage ?? "No message"}");

                if (!string.IsNullOrEmpty(span.GenAiSystem))
                {
                    sb.AppendLine($"- Provider: {span.GenAiSystem}");
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
    decimal TotalCostUsd,
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
    [property: JsonPropertyName("gen_ai_system")]
    string? GenAiSystem,
    [property: JsonPropertyName("gen_ai_request_model")]
    string? GenAiRequestModel,
    [property: JsonPropertyName("gen_ai_input_tokens")]
    long? GenAiInputTokens,
    [property: JsonPropertyName("gen_ai_output_tokens")]
    long? GenAiOutputTokens,
    [property: JsonPropertyName("gen_ai_cost_usd")]
    decimal? GenAiCostUsd);

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
