namespace qyl.mcp.Tools.Management;

using System.ComponentModel;
using System.Net.Http.Json;
using Formatting;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;

/// <summary>
///     MCP tool that lists teams with optional name search and pagination.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class ListTeamsTool(HttpClient client)
{
    /// <summary>
    ///     Lists teams with optional name/slug filtering, returning slugs that can scope members and projects.
    /// </summary>
    /// <param name="query">Optional filter for teams by name or slug.</param>
    /// <param name="limit">Maximum results per page (1-100, default 25).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted paginated list of teams.</returns>
    [QylCapability("project_and_access_management")]
    [McpServerTool(Name = "list_teams", Title = "List Teams",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("List teams with optional name search. Use team slugs to scope members and projects.")]
    public async Task<string> ListTeamsAsync(
        [Description("Filter teams by name or slug")]
        string? query = null,
        [Description("Maximum results per page (1-100, default 25)")]
        int limit = 25,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var url = $"/api/v1/mcp/teams?limit={limit}";
        if (query is not null)
            url += $"&q={Uri.EscapeDataString(query)}";

        var result = await client.GetFromJsonAsync<PagedResult<TeamDto>>(url, ct).ConfigureAwait(false);

        return result is null
            ? "No teams found."
            : ResponseFormatter.FormatPagedList(
                result,
                "Teams",
                static t =>
                {
                    var desc = t.Description is not null ? $" -- {t.Description}" : "";
                    var members = t.MemberCount is not null ? $" ({t.MemberCount} members)" : "";
                    return $"- **{t.Name}** (`{t.Slug}`){desc}{members}";
                },
                "list_teams",
                "list_teams",
                "query");
    }
}
