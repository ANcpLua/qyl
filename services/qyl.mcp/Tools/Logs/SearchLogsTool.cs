using System.ComponentModel;
using System.Net.Http.Json;
using qyl.mcp.Formatting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;

namespace qyl.mcp.Tools.Logs;

/// <summary>
///     MCP tool that searches structured logs by query with pagination support.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class SearchLogsTool(HttpClient client)
{
    /// <summary>
    ///     Searches structured logs matching a query, returning a paginated list of log entries.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <param name="projectSlug">Optional project slug filter.</param>
    /// <param name="cursor">Cursor for pagination.</param>
    /// <param name="limit">Maximum results per page (1-100, default 25).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted paginated list of matching log entries.</returns>
    [QylCapability("log_investigation")]
    [McpServerTool(Name = "search_logs", Title = "Search Logs",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
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
