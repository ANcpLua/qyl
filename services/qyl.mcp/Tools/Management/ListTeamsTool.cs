using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class ListTeamsTool(HttpClient client)
{
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
