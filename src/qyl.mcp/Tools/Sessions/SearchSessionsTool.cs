using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Sessions;

/// <summary>
/// Searches debugging sessions by query, returning a paginated list with status, service, and span count.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
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
    /// <summary>
    /// Searches debugging sessions matching the given query with paginated results.
    /// </summary>
    /// <param name="query">Search query (e.g. 'failed payments', 'service:api-gateway').</param>
    /// <param name="cursor">Optional pagination cursor from previous results.</param>
    /// <param name="limit">Maximum results per page (1-100, default 25).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted paginated list of matching sessions.</returns>
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

        return ResponseFormatter.FormatPagedList(
            result!,
            "Sessions",
            s => $"- `{s.SessionId}` | **{s.Status}** | {s.ServiceName} | {s.SpanCount} spans | {s.CreatedAt}",
            "search_sessions",
            "get_session",
            "sessionId");
    }
}
