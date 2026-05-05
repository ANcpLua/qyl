using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Sessions;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class SearchSessionsTool(HttpClient client)
{
    [McpServerTool(
        Name = "search_sessions",
        Title = "Search Sessions",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> SearchSessions(
        string query,
        string? cursor = null,
        int limit = 25,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var url = QueryString.AppendPairs(
            $"/api/v1/mcp/sessions?q={Uri.EscapeDataString(query)}&limit={limit}",
            ("cursor", cursor));

        var result = await client.GetFromJsonAsync<PagedResult<SessionSummaryDto>>(url, ct).ConfigureAwait(false);

        return ResponseFormatter.FormatPagedList(
            result!,
            "Sessions",
            s => $"- `{s.SessionId}` | **{s.Status}** | {s.ServiceName} | {s.SpanCount} spans | {s.CreatedAt}",
            "search_sessions",
            "get_session",
            "sessionId");
    }
}
