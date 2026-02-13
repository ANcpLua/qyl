using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools;

/// <summary>
///     HTTP-based telemetry store querying qyl.collector REST API.
///     Per CLAUDE.md: qyl.mcp → qyl.collector via HTTP ONLY.
/// </summary>
public sealed partial class HttpTelemetryStore(HttpClient client, TimeProvider time, ILogger<HttpTelemetryStore> logger)
    : ITelemetryStore
{
    public ValueTask RecordRunAsync(AgentRun run) => ValueTask.CompletedTask; // Read-only observation

    public async ValueTask<AgentRun?> GetRunAsync(string runId)
    {
        try
        {
            var session = await client.GetFromJsonAsync(
                $"/api/v1/sessions/{Uri.EscapeDataString(runId)}",
                HttpStoreJsonContext.Default.StoreSession).ConfigureAwait(false);

            return session is null ? null : MapToRun(session, time);
        }
        catch (HttpRequestException ex)
        {
            LogFailedGetRun(ex, runId);
            return null;
        }
    }

    public async ValueTask<AgentRun[]> SearchRunsAsync(
        string? provider, string? model, string? errorType, DateTime? since)
    {
        try
        {
            var url = "/api/v1/sessions?limit=100";
            if (!string.IsNullOrEmpty(provider))
                url += $"&provider={Uri.EscapeDataString(provider)}";

            var response = await client.GetFromJsonAsync(
                url, HttpStoreJsonContext.Default.StoreSessionList).ConfigureAwait(false);

            return response?.Items?
                .Select(s => MapToRun(s, time))
                .Where(r =>
                    (string.IsNullOrEmpty(model) ||
                     r.Model?.Contains(model, StringComparison.OrdinalIgnoreCase) is true) &&
                    (string.IsNullOrEmpty(errorType) ||
                     r.ErrorType?.Equals(errorType, StringComparison.OrdinalIgnoreCase) is true) &&
                    (!since.HasValue || r.StartedAt >= since.Value))
                .ToArray() ?? [];
        }
        catch (HttpRequestException ex)
        {
            LogFailedSearchRuns(ex);
            return [];
        }
    }

    public async ValueTask<TokenUsageSummary[]> GetTokenUsageAsync(DateTime? since, DateTime? until, string groupBy)
    {
        try
        {
            var response = await client.GetFromJsonAsync(
                "/api/v1/sessions?limit=1000",
                HttpStoreJsonContext.Default.StoreSessionList).ConfigureAwait(false);

            if (response?.Items is not { Count: > 0 }) return [];

            Func<StoreSession, string> keySelector = groupBy.ToUpperInvariant() switch
            {
                "MODEL" => s => s.Models?.FirstOrDefault() ?? "unknown",
                "HOUR" => s => ParseTime(s.StartTime)?.ToString("yyyy-MM-dd HH:00") ?? "unknown",
                _ => s => s.ServiceName ?? "unknown"
            };

            return [.. response.Items
                .Where(s => InRange(ParseTime(s.StartTime), since, until))
                .GroupBy(keySelector)
                .Select(g => new TokenUsageSummary(
                    g.Key,
                    (int)g.Sum(s => s.TotalInputTokens),
                    (int)g.Sum(s => s.TotalOutputTokens),
                    g.Count(),
                    g.Min(s => ParseTime(s.StartTime) ?? time.GetUtcNow().DateTime),
                    g.Max(s => ParseTime(s.StartTime) ?? time.GetUtcNow().DateTime)))];
        }
        catch (HttpRequestException ex)
        {
            LogFailedGetTokenUsage(ex);
            return [];
        }
    }

    public async ValueTask<AgentError[]> ListErrorsAsync(int limit, string? agentName)
    {
        try
        {
            var url = $"/api/v1/sessions?limit={limit}";
            if (!string.IsNullOrEmpty(agentName))
                url += $"&serviceName={Uri.EscapeDataString(agentName)}";

            var response = await client.GetFromJsonAsync(
                url, HttpStoreJsonContext.Default.StoreSessionList).ConfigureAwait(false);

            return response?.Items?
                .Where(s => s.ErrorCount > 0)
                .Take(limit)
                .Select(s => new AgentError(
                    s.SessionId,
                    s.ServiceName ?? "unknown",
                    "Error",
                    $"{s.ErrorCount} error(s) in session",
                    ParseTime(s.StartTime) ?? time.GetUtcNow().DateTime,
                    null))
                .ToArray() ?? [];
        }
        catch (HttpRequestException ex)
        {
            LogFailedListErrors(ex);
            return [];
        }
    }

    public async ValueTask<LatencyStats> GetLatencyStatsAsync(string? agentName, int hours)
    {
        try
        {
            var url = "/api/v1/sessions?limit=1000";
            if (!string.IsNullOrEmpty(agentName))
                url += $"&serviceName={Uri.EscapeDataString(agentName)}";

            var response = await client.GetFromJsonAsync(
                url, HttpStoreJsonContext.Default.StoreSessionList).ConfigureAwait(false);

            var since = time.GetUtcNow().AddHours(-hours).DateTime;
            var durations = response?.Items?
                .Where(s => ParseTime(s.StartTime) >= since)
                .Select(s => (double)s.SpanCount)
                .OrderBy(x => x)
                .ToList() ?? [];

            return durations.Count is 0
                ? new LatencyStats(agentName, 0, 0, 0, 0, 0, 0, 0)
                : new LatencyStats(agentName,
                    Percentile(durations, 0.50),
                    Percentile(durations, 0.95),
                    Percentile(durations, 0.99),
                    durations.Average(),
                    durations.Min(),
                    durations.Max(),
                    durations.Count);
        }
        catch (HttpRequestException ex)
        {
            LogFailedGetLatencyStats(ex);
            return new LatencyStats(agentName, 0, 0, 0, 0, 0, 0, 0);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get run {RunId} from collector")]
    private partial void LogFailedGetRun(Exception ex, string runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to search runs from collector")]
    private partial void LogFailedSearchRuns(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get token usage from collector")]
    private partial void LogFailedGetTokenUsage(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to list errors from collector")]
    private partial void LogFailedListErrors(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get latency stats from collector")]
    private partial void LogFailedGetLatencyStats(Exception ex);

    private static AgentRun MapToRun(StoreSession s, TimeProvider time)
    {
        var start = ParseTime(s.StartTime) ?? time.GetUtcNow().DateTime;
        var end = ParseTime(s.EndTime);

        return new AgentRun(
            s.SessionId,
            s.ServiceName ?? "unknown",
            s.Providers?.FirstOrDefault(),
            s.Models?.FirstOrDefault(),
            start,
            end,
            end.HasValue ? end.Value - start : null,
            (int)s.TotalInputTokens,
            (int)s.TotalOutputTokens,
            s.ErrorCount is 0,
            s.ErrorCount > 0 ? "Error" : null,
            s.ErrorCount > 0 ? $"{s.ErrorCount} error(s)" : null);
    }

    private static DateTime? ParseTime(string? s) =>
        DateTime.TryParse(s, out var dt) ? dt : null;

    private static bool InRange(DateTime? dt, DateTime? since, DateTime? until) =>
        dt.HasValue && (!since.HasValue || dt >= since) && (!until.HasValue || dt <= until);

    private static double Percentile(List<double> sorted, double p) =>
        sorted.Count is 0 ? 0 : sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];
}

#region Response DTOs

internal sealed record StoreSessionList(
    [property: JsonPropertyName("items")] List<StoreSession>? Items,
    [property: JsonPropertyName("total")] int Total);

internal sealed record StoreSession(
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
    string? StartTime,
    [property: JsonPropertyName("end_time")]
    string? EndTime,
    [property: JsonPropertyName("providers")]
    List<string>? Providers,
    [property: JsonPropertyName("models")] List<string>? Models);

#endregion

[JsonSerializable(typeof(StoreSessionList))]
[JsonSerializable(typeof(StoreSession))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class HttpStoreJsonContext : JsonSerializerContext;
