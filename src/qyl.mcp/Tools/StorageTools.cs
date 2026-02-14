using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying storage status and performing maintenance.
/// </summary>
[McpServerToolType]
public sealed class StorageTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.get_storage_stats")]
    [Description("""
                 Get storage statistics for the qyl collector.

                 Shows:
                 - Total span and log counts
                 - Number of sessions tracked
                 - Time range of stored data
                 - Database size (if available)

                 Use this to monitor storage usage and plan cleanup.

                 Returns: Storage statistics summary
                 """)]
    public async Task<string> GetStorageStatsAsync()
    {
        try
        {
            var stats = await client.GetFromJsonAsync<StorageStatsDto>(
                "/mcp/tools/call",
                StorageJsonContext.Default.StorageStatsDto).ConfigureAwait(false);

            // Try the direct health endpoint instead
            var health = await client.GetFromJsonAsync<HealthResponse>(
                "/health",
                StorageJsonContext.Default.HealthResponse).ConfigureAwait(false);

            var sb = new StringBuilder();
            sb.AppendLine("# Storage Statistics");
            sb.AppendLine();

            if (health?.Entries is not null)
            {
                foreach (var entry in health.Entries)
                {
                    sb.AppendLine($"- **{entry.Key}:** {entry.Value.Status}");
                    if (entry.Value.Data is not null)
                    {
                        foreach (var data in entry.Value.Data)
                        {
                            sb.AppendLine($"  - {data.Key}: {data.Value}");
                        }
                    }
                }
            }
            else
            {
                sb.AppendLine("Storage statistics not available from health endpoint.");
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error connecting to qyl collector: {ex.Message}";
        }
    }

    [McpServerTool(Name = "qyl.health_check")]
    [Description("""
                 Check the health status of qyl collector.

                 Returns health status of all components:
                 - DuckDB database connection
                 - Ingestion pipeline
                 - SSE streaming

                 Use this to verify qyl is running properly.

                 Returns: Health status of all components
                 """)]
    public async Task<string> HealthCheckAsync()
    {
        try
        {
            // Check multiple health endpoints concurrently
            var aliveTask = client.GetAsync("/alive");
            var readyTask = client.GetAsync("/ready");
            var healthTask = client.GetStringAsync("/health");

            var alive = await aliveTask.ConfigureAwait(false);
            var ready = await readyTask.ConfigureAwait(false);
            var healthJson = await healthTask.ConfigureAwait(false);

            var sb = new StringBuilder();
            sb.AppendLine("# qyl Collector Health Status");
            sb.AppendLine();
            sb.AppendLine($"- **Alive:** {(alive.IsSuccessStatusCode ? "OK" : "FAILED")}");
            sb.AppendLine($"- **Ready:** {(ready.IsSuccessStatusCode ? "OK" : "FAILED")}");
            sb.AppendLine();
            sb.AppendLine("## Detailed Health:");
            sb.AppendLine("```json");
            sb.AppendLine(healthJson);
            sb.AppendLine("```");

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Health check failed - qyl collector may be down: {ex.Message}";
        }
    }

    [McpServerTool(Name = "qyl.get_system_context")]
    [Description("""
                 Get pre-computed system context from qyl's insights materializer.

                 Returns a markdown document with three sections:
                 - **Topology**: Discovered services, AI models, and top errors
                 - **Performance Profile**: Latency percentiles, token spend, cost trends (7d rolling)
                 - **Known Issues**: Error spikes, cost drift, slow operations (last hour)

                 This data is refreshed every 5 minutes with zero query cost on read.
                 Use this as the first tool call to understand the system before drilling into details.

                 Returns: Markdown system context with topology, performance, and alerts
                 """)]
    public async Task<string> GetSystemContextAsync()
    {
        try
        {
            var response = await client.GetAsync("/api/v1/insights").ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return $"Insights not available (HTTP {(int)response.StatusCode}). The materializer may not have run yet.";

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("markdown", out var markdownProp))
                return markdownProp.GetString() ?? "No insights available.";

            return "Insights response did not contain expected 'markdown' field.";
        }
        catch (HttpRequestException ex)
        {
            return $"Error connecting to qyl collector: {ex.Message}";
        }
    }

    [McpServerTool(Name = "qyl.search_spans")]
    [Description("""
                 Search spans with flexible filtering.

                 This is the general-purpose span query tool. For GenAI-specific
                 queries, use list_genai_spans instead.

                 Supports filtering by:
                 - Session ID
                 - Service name
                 - Operation name (partial match)
                 - Status (ok/error)
                 - Time range

                 Example queries:
                 - Errors only: search_spans(status="error")
                 - By service: search_spans(service_name="api-gateway")
                 - By operation: search_spans(operation="HTTP GET")

                 Returns: List of matching spans with timing and attributes
                 """)]
    public async Task<string> SearchSpansAsync(
        [Description("Filter by session ID")]
        string? sessionId = null,
        [Description("Filter by service name")]
        string? serviceName = null,
        [Description("Filter by operation name (partial match)")]
        string? operation = null,
        [Description("Filter by status: 'ok' or 'error'")]
        string? status = null,
        [Description("Time window in hours (default: 24)")]
        int hours = 24,
        [Description("Maximum spans to return (default: 100)")]
        int limit = 100)
    {
        try
        {
            // Build query for the sessions/{id}/spans or general spans endpoint
            var url = !string.IsNullOrEmpty(sessionId)
                ? $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}/spans"
                : "/api/v1/genai/spans"; // Use genai/spans as general span query

            var queryParams = new List<string> { $"limit={limit}", $"hours={hours}" };
            if (!string.IsNullOrEmpty(status))
                queryParams.Add($"status={Uri.EscapeDataString(status)}");

            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);

            var response = await client.GetFromJsonAsync<SpanSearchResponse>(
                url, StorageJsonContext.Default.SpanSearchResponse).ConfigureAwait(false);

            var spans = response?.Items ?? response?.Spans;

            if (spans is null || spans.Count is 0)
                return "No spans found matching the criteria.";

            // Apply client-side filters
            if (!string.IsNullOrEmpty(serviceName))
                spans = [.. spans.Where(s =>
                    s.ServiceName?.ContainsIgnoreCase(serviceName) is true)];

            if (!string.IsNullOrEmpty(operation))
                spans = [.. spans.Where(s =>
                    s.Name?.ContainsIgnoreCase(operation) is true)];

            if (spans.Count is 0)
                return "No spans found matching the criteria after filtering.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Span Search Results ({spans.Count} spans)");
            sb.AppendLine();

            foreach (var span in spans.Take(limit))
            {
                var durationMs = span.DurationNs / 1_000_000.0;
                var statusIcon = span.StatusCode == 2 ? "[ERROR]" : "[OK]";
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(span.StartTimeUnixNano / 1_000_000);

                sb.AppendLine($"**{timestamp:HH:mm:ss}** {span.Name} {statusIcon} ({durationMs:F0}ms)");

                if (!string.IsNullOrEmpty(span.ServiceName))
                    sb.AppendLine($"  Service: {span.ServiceName}");

                if (span.StatusCode == 2 && !string.IsNullOrEmpty(span.StatusMessage))
                    sb.AppendLine($"  Error: {span.StatusMessage}");

                sb.AppendLine($"  Trace: {span.TraceId}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error connecting to qyl collector: {ex.Message}";
        }
    }
}

#region DTOs

internal sealed record StorageStatsDto(
    [property: JsonPropertyName("span_count")] long SpanCount,
    [property: JsonPropertyName("log_count")] long LogCount,
    [property: JsonPropertyName("session_count")] int SessionCount,
    [property: JsonPropertyName("oldest_timestamp")] string? OldestTimestamp,
    [property: JsonPropertyName("newest_timestamp")] string? NewestTimestamp);

internal sealed record HealthResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("entries")] Dictionary<string, HealthEntry>? Entries);

internal sealed record HealthEntry(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("data")] Dictionary<string, object>? Data);

internal sealed record SpanSearchResponse(
    [property: JsonPropertyName("items")] List<SpanSearchDto>? Items,
    [property: JsonPropertyName("spans")] List<SpanSearchDto>? Spans,
    [property: JsonPropertyName("total")] int Total);

internal sealed record SpanSearchDto(
    [property: JsonPropertyName("trace_id")] string TraceId,
    [property: JsonPropertyName("span_id")] string SpanId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("service_name")] string? ServiceName,
    [property: JsonPropertyName("start_time_unix_nano")] long StartTimeUnixNano,
    [property: JsonPropertyName("duration_ns")] long DurationNs,
    [property: JsonPropertyName("status_code")] int StatusCode,
    [property: JsonPropertyName("status_message")] string? StatusMessage);

#endregion

[JsonSerializable(typeof(StorageStatsDto))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(SpanSearchResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class StorageJsonContext : JsonSerializerContext;
