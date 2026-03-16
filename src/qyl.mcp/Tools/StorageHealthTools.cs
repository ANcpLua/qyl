using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for system health and storage status.
/// </summary>
[McpServerToolType]
public sealed class StorageHealthTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.get_storage_stats", Title = "Get Storage Stats",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
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
    public Task<string> GetStorageStatsAsync() => CollectorHelper.ExecuteAsync(async () =>
    {
        var health = await client.GetFromJsonAsync<HealthResponse>(
            "/health",
            StorageHealthJsonContext.Default.HealthResponse).ConfigureAwait(false);

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
    });

    [McpServerTool(Name = "qyl.health_check", Title = "Health Check",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Check the health status of qyl collector.

                 Returns health status of all components:
                 - DuckDB database connection
                 - Ingestion pipeline
                 - SSE streaming

                 Use this to verify qyl is running properly.

                 Returns: Health status of all components
                 """)]
    public Task<string> HealthCheckAsync() => CollectorHelper.ExecuteAsync(async () =>
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
    }, "Health check failed - qyl collector may be down");

    [McpServerTool(Name = "qyl.get_system_context", Title = "Get System Context",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
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
    public Task<string> GetSystemContextAsync() => CollectorHelper.ExecuteAsync(async () =>
    {
        var response = await client.GetAsync("/api/v1/insights").ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return
                $"Insights not available (HTTP {(int)response.StatusCode}). The materializer may not have run yet.";
        }

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("markdown", out var markdownProp))
            return markdownProp.GetString() ?? "No insights available.";

        return "Insights response did not contain expected 'markdown' field.";
    });
}

#region DTOs

internal sealed record HealthResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("entries")]
    Dictionary<string, HealthEntry>? Entries);

internal sealed record HealthEntry(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("data")] Dictionary<string, object>? Data);

#endregion

[JsonSerializable(typeof(HealthResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class StorageHealthJsonContext : JsonSerializerContext;
