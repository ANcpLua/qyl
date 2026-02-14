using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying GenAI telemetry (LLM calls, token usage, costs).
///     Provides AI-focused analytics beyond generic span queries.
/// </summary>
[McpServerToolType]
public sealed class GenAiTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.get_genai_stats")]
    [Description("""
                 Get GenAI usage statistics: total requests, tokens, and costs.

                 Aggregates across all LLM calls in the time window.
                 Use this for a quick overview of AI usage.

                 Example queries:
                 - Last 24 hours: get_genai_stats()
                 - Last week: get_genai_stats(hours=168)
                 - Specific session: get_genai_stats(session_id="abc123")

                 Returns: Request count, input/output tokens, total cost USD
                 """)]
    public Task<string> GetGenAiStatsAsync(
        [Description("Filter by session ID")]
        string? sessionId = null,
        [Description("Time window in hours (default: 24)")]
        int hours = 24)
    {
        return CollectorHelper.ExecuteAsync(async () =>
        {
            var url = $"/api/v1/genai/stats?hours={hours}";
            if (!string.IsNullOrEmpty(sessionId))
                url += $"&sessionId={Uri.EscapeDataString(sessionId)}";

            var stats = await client.GetFromJsonAsync<GenAiStatsDto>(
                url, GenAiJsonContext.Default.GenAiStatsDto).ConfigureAwait(false);

            if (stats is null)
                return "No GenAI statistics available.";

            var sb = new StringBuilder();
            sb.AppendLine($"# GenAI Statistics (last {hours} hours)");
            if (!string.IsNullOrEmpty(sessionId))
                sb.AppendLine($"Session: {sessionId}");
            sb.AppendLine();
            sb.AppendLine($"- **Requests:** {stats.RequestCount:N0}");
            sb.AppendLine($"- **Input tokens:** {stats.TotalInputTokens:N0}");
            sb.AppendLine($"- **Output tokens:** {stats.TotalOutputTokens:N0}");
            sb.AppendLine($"- **Total cost:** ${stats.TotalCostUsd:F4}");

            if (stats.AvgLatencyMs > 0)
                sb.AppendLine($"- **Avg latency:** {stats.AvgLatencyMs:F0}ms");

            if (stats.ErrorCount > 0)
                sb.AppendLine($"- **Errors:** {stats.ErrorCount:N0}");

            return sb.ToString();
        });
    }

    [McpServerTool(Name = "qyl.list_genai_spans")]
    [Description("""
                 List GenAI spans (LLM calls) with filtering.

                 Shows individual AI requests with:
                 - Provider and model
                 - Token counts and cost
                 - Duration and status
                 - Operation type (chat, embeddings, etc.)

                 Example queries:
                 - All recent: list_genai_spans()
                 - By provider: list_genai_spans(provider="anthropic")
                 - By model: list_genai_spans(model="claude-3")
                 - Errors only: list_genai_spans(status="error")

                 Returns: List of GenAI spans with full details
                 """)]
    public Task<string> ListGenAiSpansAsync(
        [Description("Filter by provider: 'openai', 'anthropic', 'google', 'azure'")]
        string? provider = null,
        [Description("Filter by model name (partial match, e.g., 'claude-3')")]
        string? model = null,
        [Description("Filter by status: 'ok' or 'error'")]
        string? status = null,
        [Description("Filter by session ID")]
        string? sessionId = null,
        [Description("Maximum spans to return (default: 50)")]
        int limit = 50)
    {
        return CollectorHelper.ExecuteAsync(async () =>
        {
            var url = $"/api/v1/genai/spans?limit={limit}";
            if (!string.IsNullOrEmpty(provider))
                url += $"&provider={Uri.EscapeDataString(provider)}";
            if (!string.IsNullOrEmpty(model))
                url += $"&model={Uri.EscapeDataString(model)}";
            if (!string.IsNullOrEmpty(status))
                url += $"&status={Uri.EscapeDataString(status)}";
            if (!string.IsNullOrEmpty(sessionId))
                url += $"&sessionId={Uri.EscapeDataString(sessionId)}";

            var response = await client.GetFromJsonAsync<GenAiSpansResponse>(
                url, GenAiJsonContext.Default.GenAiSpansResponse).ConfigureAwait(false);

            if (response?.Spans is null || response.Spans.Count is 0)
                return "No GenAI spans found matching the criteria.";

            var sb = new StringBuilder();
            sb.AppendLine($"# GenAI Spans ({response.Spans.Count} results)");
            sb.AppendLine();

            foreach (var span in response.Spans)
            {
                var durationMs = span.DurationNs / 1_000_000.0;
                var statusIcon = span.StatusCode == 2 ? "[ERROR]" : "[OK]";
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(span.StartTimeUnixNano / 1_000_000);

                sb.AppendLine($"## {span.Name} {statusIcon}");
                sb.AppendLine($"- Time: {timestamp:HH:mm:ss}");
                sb.AppendLine($"- Provider: {span.GenAiProviderName ?? "unknown"}");
                sb.AppendLine($"- Model: {span.GenAiRequestModel ?? "unknown"}");
                sb.AppendLine($"- Duration: {durationMs:F0}ms");

                if (span.GenAiInputTokens > 0 || span.GenAiOutputTokens > 0)
                    sb.AppendLine($"- Tokens: {span.GenAiInputTokens} in / {span.GenAiOutputTokens} out");

                if (span.GenAiCostUsd > 0)
                    sb.AppendLine($"- Cost: ${span.GenAiCostUsd:F6}");

                if (span.StatusCode == 2 && !string.IsNullOrEmpty(span.StatusMessage))
                    sb.AppendLine($"- Error: {span.StatusMessage}");

                sb.AppendLine($"- Trace: {span.TraceId}");
                sb.AppendLine();
            }

            return sb.ToString();
        });
    }

    [McpServerTool(Name = "qyl.list_models")]
    [Description("""
                 Get usage breakdown by AI model.

                 Shows which models are being used and their costs.
                 Useful for understanding model selection and optimizing costs.

                 Returns: List of models with request counts, tokens, and costs
                 """)]
    public Task<string> ListModelsAsync(
        [Description("Time window in hours (default: 24)")]
        int hours = 24)
    {
        return CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<ModelUsageResponse>(
                $"/api/v1/genai/models?hours={hours}",
                GenAiJsonContext.Default.ModelUsageResponse).ConfigureAwait(false);

            if (response?.Models is null || response.Models.Count is 0)
                return "No model usage data available.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Model Usage (last {hours} hours)");
            sb.AppendLine();
            sb.AppendLine("| Model | Requests | Input Tokens | Output Tokens | Cost |");
            sb.AppendLine("|-------|----------|--------------|---------------|------|");

            foreach (var model in response.Models.OrderByDescending(static m => m.TotalCostUsd))
            {
                sb.AppendLine($"| {model.ModelName} | {model.RequestCount:N0} | {model.TotalInputTokens:N0} | {model.TotalOutputTokens:N0} | ${model.TotalCostUsd:F4} |");
            }

            sb.AppendLine();
            sb.AppendLine($"**Total Cost:** ${response.Models.Sum(static m => m.TotalCostUsd):F4}");

            return sb.ToString();
        });
    }

    [McpServerTool(Name = "qyl.get_token_timeseries")]
    [Description("""
                 Get token usage over time for trend analysis.

                 Shows how token consumption varies by hour/day.
                 Useful for identifying usage patterns and spikes.

                 Parameters:
                 - hours: Time window (default: 24)
                 - interval: 'hour' or 'day' (default: hour)

                 Returns: Time series of token usage with costs
                 """)]
    public Task<string> GetTokenTimeseriesAsync(
        [Description("Time window in hours (default: 24)")]
        int hours = 24,
        [Description("Aggregation interval: 'hour' or 'day'")]
        string interval = "hour")
    {
        return CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<TokenTimeseriesResponse>(
                $"/api/v1/genai/usage/timeseries?hours={hours}&interval={interval}",
                GenAiJsonContext.Default.TokenTimeseriesResponse).ConfigureAwait(false);

            if (response?.Data is null || response.Data.Count is 0)
                return "No usage data available for the specified time range.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Token Usage Timeseries (last {hours} hours, by {interval})");
            sb.AppendLine();
            sb.AppendLine("| Time | Input | Output | Cost |");
            sb.AppendLine("|------|-------|--------|------|");

            foreach (var point in response.Data)
            {
                var time = DateTimeOffset.FromUnixTimeMilliseconds(point.TimestampMs);
                var timeStr = interval == "day" ? time.ToString("MM-dd") : time.ToString("HH:mm");
                sb.AppendLine($"| {timeStr} | {point.InputTokens:N0} | {point.OutputTokens:N0} | ${point.CostUsd:F4} |");
            }

            return sb.ToString();
        });
    }
}

#region DTOs

internal sealed record GenAiStatsDto(
    [property: JsonPropertyName("request_count")] int RequestCount,
    [property: JsonPropertyName("total_input_tokens")] long TotalInputTokens,
    [property: JsonPropertyName("total_output_tokens")] long TotalOutputTokens,
    [property: JsonPropertyName("total_cost_usd")] double TotalCostUsd,
    [property: JsonPropertyName("avg_latency_ms")] double AvgLatencyMs,
    [property: JsonPropertyName("error_count")] int ErrorCount);

internal sealed record GenAiSpansResponse(
    [property: JsonPropertyName("spans")] List<GenAiSpanDto>? Spans,
    [property: JsonPropertyName("total")] int Total);

internal sealed record GenAiSpanDto(
    [property: JsonPropertyName("trace_id")] string TraceId,
    [property: JsonPropertyName("span_id")] string SpanId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("start_time_unix_nano")] long StartTimeUnixNano,
    [property: JsonPropertyName("duration_ns")] long DurationNs,
    [property: JsonPropertyName("status_code")] int StatusCode,
    [property: JsonPropertyName("status_message")] string? StatusMessage,
    [property: JsonPropertyName("gen_ai_provider_name")] string? GenAiProviderName,
    [property: JsonPropertyName("gen_ai_request_model")] string? GenAiRequestModel,
    [property: JsonPropertyName("gen_ai_input_tokens")] long? GenAiInputTokens,
    [property: JsonPropertyName("gen_ai_output_tokens")] long? GenAiOutputTokens,
    [property: JsonPropertyName("gen_ai_cost_usd")] double? GenAiCostUsd);

internal sealed record ModelUsageResponse(
    [property: JsonPropertyName("models")] List<ModelUsageDto>? Models);

internal sealed record ModelUsageDto(
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("request_count")] int RequestCount,
    [property: JsonPropertyName("total_input_tokens")] long TotalInputTokens,
    [property: JsonPropertyName("total_output_tokens")] long TotalOutputTokens,
    [property: JsonPropertyName("total_cost_usd")] double TotalCostUsd);

internal sealed record TokenTimeseriesResponse(
    [property: JsonPropertyName("data")] List<TokenTimeseriesPoint>? Data);

internal sealed record TokenTimeseriesPoint(
    [property: JsonPropertyName("timestamp_ms")] long TimestampMs,
    [property: JsonPropertyName("input_tokens")] long InputTokens,
    [property: JsonPropertyName("output_tokens")] long OutputTokens,
    [property: JsonPropertyName("cost_usd")] double CostUsd);

#endregion

[JsonSerializable(typeof(GenAiStatsDto))]
[JsonSerializable(typeof(GenAiSpansResponse))]
[JsonSerializable(typeof(ModelUsageResponse))]
[JsonSerializable(typeof(TokenTimeseriesResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class GenAiJsonContext : JsonSerializerContext;
