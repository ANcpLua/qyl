using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Discovery;

/// <summary>
/// MCP tool that retrieves the service dependency map showing nodes and edges between services.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class GetServiceMapTool(HttpClient client)
{
    /// <summary>
    /// Retrieves the service dependency map with service nodes and inter-service call edges.
    /// </summary>
    /// <param name="projectSlug">Optional project slug filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown string containing the service map topology.</returns>
    [QylCapability("service_discovery", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_service_map", Title = "Get Service Map",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Get the service dependency map showing nodes (services) and edges (calls between services).")]
    public async Task<string> GetServiceMapAsync(
        [Description("Filter by project slug")]
        string? projectSlug = null,
        CancellationToken ct = default)
    {
        var url = "/api/v1/mcp/services/map";
        if (projectSlug is not null)
            url += $"?project={Uri.EscapeDataString(projectSlug)}";

        var map = await client.GetFromJsonAsync<ServiceMapDto>(url, ct).ConfigureAwait(false);

        if (map is null or { Nodes.Count: 0 })
            return "No service map data available yet. Service maps are built from trace data.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Service Map ({map.Nodes.Count} nodes, {map.Edges.Count} edges)");
        sb.AppendLine();

        sb.AppendLine("## Nodes");
        sb.AppendLine();
        sb.AppendLine("| Service | Type |");
        sb.AppendLine("|---------|------|");
        foreach (var node in map.Nodes)
            sb.AppendLine($"| {node.Name} | {node.Type} |");

        sb.AppendLine();

        if (map.Edges is { Count: > 0 })
        {
            sb.AppendLine("## Edges");
            sb.AppendLine();
            sb.AppendLine("| Source | Target | Calls |");
            sb.AppendLine("|--------|--------|-------|");
            foreach (var edge in map.Edges)
                sb.AppendLine($"| {edge.Source} | {edge.Target} | {edge.CallCount} |");
        }

        return sb.ToString();
    }
}
