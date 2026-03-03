using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace qyl.mcp.Tools;

[McpServerToolType]
public sealed class ServiceTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_services", Title = "List Services",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("List all detected services with type, status, instance count, and aggregated telemetry stats")]
    public Task<string> ListServicesAsync(
        [Description("Filter by service type: ai_agent, llm_provider, mcp_server, traditional")] string? type = null,
        [Description("Filter by status: active, inactive")] string? status = null,
        [Description("Maximum services to return (default: 50)")] int limit = 50) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var query = $"?limit={limit}";
            if (type is not null) query += $"&type={type}";
            if (status is not null) query += $"&status={status}";

            var response = await client.GetFromJsonAsync<ServicesMcpResponse>(
                $"/api/v1/services{query}",
                ServiceMcpJsonContext.Default.ServicesMcpResponse).ConfigureAwait(false);

            if (response?.Services is not { Count: > 0 })
                return "No services detected yet. Services are auto-discovered from incoming OTLP telemetry.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Detected Services ({response.Total})");
            sb.AppendLine();
            sb.AppendLine("| Service | Type | Status | Instances | Spans | Errors | Error Rate |");
            sb.AppendLine("|---------|------|--------|-----------|-------|--------|------------|");

            foreach (var s in response.Services)
            {
                var statusIndicator = s.ActiveInstances > 0 ? "active" : "inactive";
                var errorRate = s.ErrorRate.HasValue ? $"{s.ErrorRate.Value:P1}" : "—";
                sb.AppendLine($"| {s.ServiceName} | {s.ServiceType} | {statusIndicator} | {s.TotalInstances} | {s.TotalSpans:N0} | {s.TotalErrors:N0} | {errorRate} |");
            }

            return sb.ToString();
        });
}

// ═════════════════════════════════════════════════════════════════════════════
// MCP DTOs (separate from collector DTOs for AOT isolation)
// ═════════════════════════════════════════════════════════════════════════════

internal sealed record ServiceMcpSummary
{
    [JsonPropertyName("serviceNamespace")] public string ServiceNamespace { get; init; } = "";
    [JsonPropertyName("serviceName")] public string ServiceName { get; init; } = "";
    [JsonPropertyName("serviceType")] public string ServiceType { get; init; } = "traditional";
    [JsonPropertyName("latestVersion")] public string? LatestVersion { get; init; }
    [JsonPropertyName("providerName")] public string? ProviderName { get; init; }
    [JsonPropertyName("defaultModel")] public string? DefaultModel { get; init; }
    [JsonPropertyName("totalInstances")] public int TotalInstances { get; init; }
    [JsonPropertyName("activeInstances")] public int ActiveInstances { get; init; }
    [JsonPropertyName("totalSpans")] public long TotalSpans { get; init; }
    [JsonPropertyName("totalErrors")] public long TotalErrors { get; init; }
    [JsonPropertyName("errorRate")] public double? ErrorRate { get; init; }
}

internal sealed record ServicesMcpResponse
{
    [JsonPropertyName("services")] public IReadOnlyList<ServiceMcpSummary> Services { get; init; } = [];
    [JsonPropertyName("total")] public int Total { get; init; }
}

// ═════════════════════════════════════════════════════════════════════════════
// JSON Context (AOT)
// ═════════════════════════════════════════════════════════════════════════════

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ServiceMcpSummary))]
[JsonSerializable(typeof(ServicesMcpResponse))]
internal sealed partial class ServiceMcpJsonContext : JsonSerializerContext;
