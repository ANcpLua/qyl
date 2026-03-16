using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Discovery;

[McpServerToolType]
public sealed class ListProjectsTool(HttpClient client)
{
    [McpServerTool(Name = "list_projects", Title = "List Projects",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("List available projects. Use project slugs to scope other tools to a specific project.")]
    public async Task<string> ListProjectsAsync(
        [Description("Cursor for pagination")] string? cursor = null,
        [Description("Maximum results per page (1-100, default 25)")]
        int limit = 25,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var url = $"/api/v1/mcp/projects?limit={limit}";
        if (cursor is not null)
            url += $"&cursor={Uri.EscapeDataString(cursor)}";

        var result = await client.GetFromJsonAsync<PagedResult<ProjectInfoDto>>(url, ct).ConfigureAwait(false);

        return result is null
            ? "No projects found."
            : ResponseFormatter.FormatPagedList(
                result,
                "Projects",
                static p =>
                {
                    var desc = p.Description is not null ? $" -- {p.Description}" : "";
                    var retention = p.RetentionDays is not null ? $" ({p.RetentionDays}d retention)" : "";
                    return $"- **{p.Name}** (`{p.Slug}`){desc}{retention}";
                },
                "list_projects",
                "search_traces",
                "projectSlug");
    }
}
