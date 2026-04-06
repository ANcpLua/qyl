using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Sessions;

[McpServerToolType]
public sealed class SearchSessionsTool(HttpClient client)
{
    [McpServerTool(
        Name = "search_sessions",
        Title = "Search Sessions",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = true)]
    [Description(
        "Search debugging sessions by query. Returns paginated list of sessions with status, service, and span count.")]
    public async Task<string> SearchSessions(
        [Description("Search query (e.g. 'failed payments', 'service:api-gateway')")]
        string query,
        [Description("Pagination cursor from previous results")]
        string? cursor = null,
        [Description("Max results per page (default 25, max 100)")]
        int limit = 25,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var url = $"/api/v1/mcp/sessions?q={Uri.EscapeDataString(query)}&limit={limit}";
        if (cursor is not null)
            url += $"&cursor={Uri.EscapeDataString(cursor)}";

        var result = await client.GetFromJsonAsync<PagedResult<SessionSummaryDto>>(url, ct).ConfigureAwait(false);

        if (result is null)
            return "No sessions found matching the query.";

        return ResponseFormatter.FormatPagedList(
            result,
            "Sessions",
            s => $"- `{s.SessionId}` | **{s.Status}** | {s.ServiceName} | {s.SpanCount} spans | {s.CreatedAt}",
            "search_sessions",
            "get_session",
            "sessionId");
    }
}
