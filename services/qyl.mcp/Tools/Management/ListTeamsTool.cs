using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

/// <summary>
///     MCP tool that lists teams with optional name search and pagination.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class ListTeamsTool(HttpClient client)
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
    public async partial Task<string> ListTeamsAsync(
        string? query = null,
        int limit = 25,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var url = QueryString.AppendPairs(
            $"/api/v1/mcp/teams?limit={limit}", ("q", query));

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
