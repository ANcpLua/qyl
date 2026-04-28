using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Discovery;

/// <summary>
///     MCP tool that lists available projects with pagination support.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class ListProjectsTool(HttpClient client)
{
    /// <summary>
    ///     Lists available projects, returning slugs that can scope other tools.
    /// </summary>
    /// <param name="cursor">Cursor for pagination.</param>
    /// <param name="limit">Maximum results per page (1-100, default 25).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted paginated list of projects.</returns>
    [QylCapability("service_discovery")]
    [McpServerTool(Name = "list_projects", Title = "List Projects",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public async partial Task<string> ListProjectsAsync(
        string? cursor = null,
        int limit = 25,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var url = QueryString.AppendPairs(
            $"/api/v1/mcp/projects?limit={limit}", ("cursor", cursor));

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
