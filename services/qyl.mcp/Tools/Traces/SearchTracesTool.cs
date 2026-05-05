using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Traces;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class SearchTracesTool(HttpClient client)
{
    [QylCapability("trace_investigation")]
    [QylCapability("anomaly_detection", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "search_traces", Title = "Search Traces",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> SearchTracesAsync(
        string query,
        string? projectSlug = null,
        string? cursor = null,
        int limit = 25,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var url = QueryString.AppendPairs(
            $"/api/v1/mcp/traces?q={Uri.EscapeDataString(query)}&limit={limit}",
            ("project", projectSlug), ("cursor", cursor));

        var result = await client.GetFromJsonAsync<PagedResult<TraceSummaryDto>>(url, ct).ConfigureAwait(false);

        return result is null
            ? "No traces found matching the query."
            : ResponseFormatter.FormatPagedList(
                result,
                "Trace Search Results",
                static t =>
                    $"- `{t.TraceId}` | **{t.RootSpan}** | {t.Service} | {t.Status} | {t.DurationMs:F1}ms | {t.SpanCount} spans | {t.StartTime}",
                "search_traces",
                "get_trace_details",
                "traceId");
    }
}
