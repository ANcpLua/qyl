using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Discovery;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class GetServiceMapTool(HttpClient client)
{
    [QylCapability("service_discovery", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_service_map", Title = "Get Service Map",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public async partial Task<string> GetServiceMapAsync(
        string? projectSlug = null,
        CancellationToken ct = default)
    {
        var url = QueryString.AppendPairs(
            "/api/v1/mcp/services/map", ("project", projectSlug));

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
