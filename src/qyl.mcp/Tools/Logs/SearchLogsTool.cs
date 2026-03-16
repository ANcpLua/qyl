using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Logs;

[McpServerToolType]
public sealed class SearchLogsTool(HttpClient client)
{
    [McpServerTool(Name = "search_logs", Title = "Search Logs",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Search structured logs by query. Returns a paginated list of matching log entries.")]
    public async Task<string> SearchLogsAsync(
        [Description("Search query (required)")]
        string query,
        [Description("Filter by project slug")]
        string? projectSlug = null,
        [Description("Cursor for pagination")] string? cursor = null,
        [Description("Maximum results per page (1-100, default 25)")]
        int limit = 25,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var url = $"/api/v1/mcp/logs?q={Uri.EscapeDataString(query)}&limit={limit}";
        if (projectSlug is not null)
            url += $"&project={Uri.EscapeDataString(projectSlug)}";
        if (cursor is not null)
            url += $"&cursor={Uri.EscapeDataString(cursor)}";

        var result = await client.GetFromJsonAsync<PagedResult<LogSummaryDto>>(url, ct).ConfigureAwait(false);

        return result is null
            ? "No logs found matching the query."
            : ResponseFormatter.FormatPagedList(
                result,
                "Log Search Results",
                static l => $"- `{l.LogId}` | [{l.Severity}] {l.Body} | {l.ServiceName} | {l.Timestamp}",
                "search_logs",
                "get_log_details",
                "logId");
    }
}
