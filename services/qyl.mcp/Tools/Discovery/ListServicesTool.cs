using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Discovery;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class ListServicesTool(HttpClient client)
{
    [QylCapability("service_discovery")]
    [McpServerTool(Name = "list_services", Title = "List Services",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public async partial Task<string> ListServicesAsync(
        string? projectSlug = null,
        CancellationToken ct = default)
    {
        var url = QueryString.AppendPairs(
            "/api/v1/mcp/services", ("project", projectSlug));

        var services = await client.GetFromJsonAsync<IReadOnlyList<ServiceInfoDto>>(url, ct).ConfigureAwait(false);

        if (services is null or { Count: 0 })
            return "No services detected yet. Services are auto-discovered from incoming telemetry.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Services ({services.Count})");
        sb.AppendLine();
        sb.AppendLine("| Service | Instances | Last Seen |");
        sb.AppendLine("|---------|-----------|-----------|");

        foreach (var svc in services)
            sb.AppendLine($"| {svc.Name} | {svc.InstanceCount} | {svc.LastSeen} |");

        sb.AppendLine();
        sb.AppendLine("## Next steps");
        sb.AppendLine("- Use `search_traces(query: '<service name>')` to find traces for a service");
        sb.AppendLine("- Use `get_service_map()` to visualize service dependencies");

        return sb.ToString();
    }
}
