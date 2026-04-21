using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for system health and storage status.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Health)]
public sealed class StorageHealthTools(HttpClient client)
{
    /// <summary>Retrieves storage statistics for the qyl collector including span/log counts and database size.</summary>
    /// <returns>A storage statistics summary from the health endpoint.</returns>
    [QylCapability("health_and_storage", QylCapabilityRole.FollowUp)]
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
        // /health is a bare 200/503 Aspire-style probe — rich component detail lives on /health/ui.
        var health = await client.GetFromJsonAsync<HealthUiResponsePayload>(
            "/health/ui",
            StorageHealthJsonContext.Default.HealthUiResponsePayload).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("# Storage Statistics");
        sb.AppendLine();

        if (health?.Components is { Count: > 0 } components)
        {
            foreach (var component in components)
            {
                sb.AppendLine($"- **{component.Name}:** {component.Status}");
                if (component.Data is not null)
                {
                    foreach (var data in component.Data)
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

    /// <summary>Checks the health status of all qyl collector components.</summary>
    /// <returns>Health status of DuckDB, ingestion pipeline, and SSE streaming.</returns>
    [QylCapability("health_and_storage")]
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
        // Aspire-style probes: /alive = live-tagged, /health = ready-tagged. Rich detail from /health/ui.
        var aliveTask = client.GetAsync("/alive");
        var healthTask = client.GetAsync("/health");
        var detailTask = client.GetFromJsonAsync<HealthUiResponsePayload>(
            "/health/ui", StorageHealthJsonContext.Default.HealthUiResponsePayload);

        var alive = await aliveTask.ConfigureAwait(false);
        var health = await healthTask.ConfigureAwait(false);
        var detail = await detailTask.ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("# qyl Collector Health Status");
        sb.AppendLine();
        sb.AppendLine($"- **Alive (live probe):** {(alive.IsSuccessStatusCode ? "OK" : "FAILED")}");
        sb.AppendLine($"- **Ready (health probe):** {(health.IsSuccessStatusCode ? "OK" : "FAILED")}");

        if (detail?.Components is { Count: > 0 } components)
        {
            sb.AppendLine();
            sb.AppendLine("## Components");
            foreach (var component in components)
            {
                sb.AppendLine($"- **{component.Name}:** {component.Status}"
                              + (string.IsNullOrWhiteSpace(component.Message)
                                  ? string.Empty
                                  : $" — {component.Message}"));
            }
        }

        return sb.ToString();
    }, "Health check failed - qyl collector may be down");

    /// <summary>Retrieves pre-computed system context (topology, performance, known issues) from the insights materializer.</summary>
    /// <returns>Markdown system context with topology, performance profile, and alerts.</returns>
    [QylCapability("health_and_storage")]
    [McpServerTool(Name = "qyl.get_system_context", Title = "Get System Context",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
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

// Mirrors Qyl.Collector.Health.HealthUiResponse JSON (camelCase via collector's QylOptions).
// Kept local so qyl.mcp doesn't take a project reference on qyl.collector.
internal sealed record HealthUiResponsePayload(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("components")]
    IReadOnlyList<ComponentHealthPayload>? Components,
    [property: JsonPropertyName("uptimeSeconds")]
    long UptimeSeconds,
    [property: JsonPropertyName("version")]
    string? Version);

internal sealed record ComponentHealthPayload(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")]
    string? Message,
    [property: JsonPropertyName("data")] Dictionary<string, object>? Data);

#endregion

[JsonSerializable(typeof(HealthUiResponsePayload))]
internal sealed partial class StorageHealthJsonContext : JsonSerializerContext;
