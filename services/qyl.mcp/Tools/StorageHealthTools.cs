using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Health)]
public sealed partial class StorageHealthTools(HttpClient client)
{
    [QylCapability("health_and_storage", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.get_storage_stats", Title = "Get Storage Stats",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public partial Task<string> GetStorageStatsAsync() => CollectorHelper.ExecuteAsync(async () =>
    {
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

    [QylCapability("health_and_storage")]
    [McpServerTool(Name = "qyl.health_check", Title = "Health Check",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public partial Task<string> HealthCheckAsync() => CollectorHelper.ExecuteAsync(async () =>
    {
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

    [QylCapability("health_and_storage")]
    [McpServerTool(Name = "qyl.get_system_context", Title = "Get System Context",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public partial Task<string> GetSystemContextAsync() => CollectorHelper.ExecuteAsync(async () =>
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
