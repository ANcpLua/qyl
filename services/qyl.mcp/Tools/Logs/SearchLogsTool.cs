using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Logs;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class SearchLogsTool(HttpClient client)
{
    [QylCapability("log_investigation")]
    [McpServerTool(Name = "search_logs", Title = "Search Logs",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> SearchLogsAsync(
        string query,
        string? projectSlug = null,
        string? cursor = null,
        int limit = 25,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var url = QueryString.AppendPairs(
            $"/api/v1/mcp/logs?q={Uri.EscapeDataString(query)}&limit={limit}",
            ("project", projectSlug), ("cursor", cursor));

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
