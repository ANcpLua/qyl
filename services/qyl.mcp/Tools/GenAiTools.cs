using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Contracts.Primitives;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying GenAI telemetry (LLM calls, token usage, costs).
///     Provides AI-focused analytics beyond generic span queries.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class GenAiTools(HttpClient client)
{
    /// <summary>Retrieves aggregate GenAI usage statistics: requests, tokens, and costs.</summary>
    /// <param name="sessionId">Optional session ID filter.</param>
    /// <param name="hours">Time window in hours.</param>
    /// <returns>Request count, input/output tokens, total cost, and error count.</returns>
    [QylCapability("genai_observability")]
    [McpServerTool(Name = "qyl.get_genai_stats", Title = "Get GenAI Stats",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public partial Task<string> GetGenAiStatsAsync(
        string? sessionId = null,
        int hours = 24) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var url = QueryString.AppendPairs(
                $"/api/v1/genai/stats?hours={hours}", ("sessionId", sessionId));

            if (await client.GetFromJsonAsync<GenAiStatsDto>(url, GenAiJsonContext.Default.GenAiStatsDto)
                    .ConfigureAwait(false) is not { } stats)
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

    /// <summary>Lists GenAI spans (individual LLM calls) with optional filtering.</summary>
    /// <param name="provider">Filter by provider name (e.g. 'anthropic', 'openai').</param>
    /// <param name="model">Filter by model name with partial matching.</param>
    /// <param name="status">Filter by status: 'ok' or 'error'.</param>
    /// <param name="sessionId">Filter by session ID.</param>
    /// <param name="limit">Maximum number of spans to return.</param>
    /// <returns>A list of GenAI spans with provider, model, tokens, cost, and status.</returns>
    [QylCapability("genai_observability")]
    [McpServerTool(Name = "qyl.list_genai_spans", Title = "List GenAI Spans",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> ListGenAiSpansAsync(
        string? provider = null,
        string? model = null,
        string? status = null,
        string? sessionId = null,
        int limit = 50) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var url = QueryString.AppendPairs(
                $"/api/v1/genai/spans?limit={limit}",
                ("provider", provider), ("model", model),
                ("status", status), ("sessionId", sessionId));

            var response = await client.GetFromJsonAsync<GenAiSpansResponse>(
                url, GenAiJsonContext.Default.GenAiSpansResponse).ConfigureAwait(false);

            if (response?.Spans is null || response.Spans.Count is 0)
                return "No GenAI spans found matching the criteria.";

            var sb = new StringBuilder();
            sb.AppendLine($"# GenAI Spans ({response.Spans.Count} results)");
            sb.AppendLine();

            foreach (var span in response.Spans)
            {
                var durationMs = TimeConversions.NanosToMs(span.DurationNs);
                var statusIcon = span.StatusCode == 2 ? "[ERROR]" : "[OK]";
                var timestamp = TimeConversions.NanosToDateTimeOffset(span.StartTimeUnixNano);

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

    /// <summary>Retrieves usage breakdown by AI model with request counts, tokens, and costs.</summary>
    /// <param name="hours">Time window in hours.</param>
    /// <returns>A table of models ranked by cost with request counts and token usage.</returns>
    // TODO: Endpoint /api/v1/genai/models missing in collector — see docs/mcp-tool-audit.md
    [McpServerTool(Name = "qyl.list_models", Title = "List Models",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> ListModelsAsync(
        int hours = 24) =>
        CollectorHelper.ExecuteAsync(async () =>
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
                sb.AppendLine(
                    $"| {model.ModelName} | {model.RequestCount:N0} | {model.TotalInputTokens:N0} | {model.TotalOutputTokens:N0} | ${model.TotalCostUsd:F4} |");
            }

            sb.AppendLine();
            sb.AppendLine($"**Total Cost:** ${response.Models.Sum(static m => m.TotalCostUsd):F4}");

            return sb.ToString();
        });

    /// <summary>Retrieves token usage over time as a time series for trend analysis.</summary>
    /// <param name="hours">Time window in hours.</param>
    /// <param name="interval">Aggregation interval: 'hour' or 'day'.</param>
    /// <returns>Time series of token usage with costs per interval.</returns>
    // TODO: Endpoint /api/v1/genai/usage/timeseries missing in collector — see docs/mcp-tool-audit.md
    [QylCapability("genai_observability", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.get_token_timeseries", Title = "Get Token Timeseries",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> GetTokenTimeseriesAsync(
        int hours = 24,
        string interval = "hour") =>
        CollectorHelper.ExecuteAsync(async () =>
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
                sb.AppendLine(
                    $"| {timeStr} | {point.InputTokens:N0} | {point.OutputTokens:N0} | ${point.CostUsd:F4} |");
            }

            return sb.ToString();
        });
}

#region DTOs

internal sealed record GenAiStatsDto(
    [property: JsonPropertyName("request_count")]
    int RequestCount,
    [property: JsonPropertyName("total_input_tokens")]
    long TotalInputTokens,
    [property: JsonPropertyName("total_output_tokens")]
    long TotalOutputTokens,
    [property: JsonPropertyName("total_cost_usd")]
    double TotalCostUsd,
    [property: JsonPropertyName("avg_latency_ms")]
    double AvgLatencyMs,
    [property: JsonPropertyName("error_count")]
    int ErrorCount);

internal sealed record GenAiSpansResponse(
    [property: JsonPropertyName("spans")] List<GenAiSpanDto>? Spans,
    [property: JsonPropertyName("total")] int Total);

internal sealed record GenAiSpanDto(
    [property: JsonPropertyName("trace_id")]
    string TraceId,
    [property: JsonPropertyName("span_id")]
    string SpanId,
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

internal sealed record ModelUsageResponse(
    [property: JsonPropertyName("models")] List<ModelUsageDto>? Models);

internal sealed record ModelUsageDto(
    [property: JsonPropertyName("model_name")]
    string ModelName,
    [property: JsonPropertyName("provider")]
    string? Provider,
    [property: JsonPropertyName("request_count")]
    int RequestCount,
    [property: JsonPropertyName("total_input_tokens")]
    long TotalInputTokens,
    [property: JsonPropertyName("total_output_tokens")]
    long TotalOutputTokens,
    [property: JsonPropertyName("total_cost_usd")]
    double TotalCostUsd);

internal sealed record TokenTimeseriesResponse(
    [property: JsonPropertyName("data")] List<TokenTimeseriesPoint>? Data);

internal sealed record TokenTimeseriesPoint(
    [property: JsonPropertyName("timestamp_ms")]
    long TimestampMs,
    [property: JsonPropertyName("input_tokens")]
    long InputTokens,
    [property: JsonPropertyName("output_tokens")]
    long OutputTokens,
    [property: JsonPropertyName("cost_usd")]
    double CostUsd);

#endregion

[JsonSerializable(typeof(GenAiStatsDto))]
[JsonSerializable(typeof(GenAiSpansResponse))]
[JsonSerializable(typeof(ModelUsageResponse))]
[JsonSerializable(typeof(TokenTimeseriesResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class GenAiJsonContext : JsonSerializerContext;
