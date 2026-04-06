using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Discovery;

[McpServerToolType]
public sealed class ListServicesTool(HttpClient client)
{
    [McpServerTool(Name = "list_services", Title = "List Services",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("List detected services with instance count and last-seen timestamp. Optionally filter by project.")]
    public async Task<string> ListServicesAsync(
        [Description("Filter by project slug")]
        string? projectSlug = null,
        CancellationToken ct = default)
    {
        var url = "/api/v1/mcp/services";
        if (projectSlug is not null)
            url += $"?project={Uri.EscapeDataString(projectSlug)}";

        var services = await client.GetFromJsonAsync(url, CollectorDtoJsonContext.Default.ListServiceInfoDto, ct).ConfigureAwait(false);

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
