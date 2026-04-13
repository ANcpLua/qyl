using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using Qyl.Contracts.Models;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Traces;

/// <summary>
/// Searches distributed traces by query, returning a paginated list with duration, status, and root span.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class SearchTracesTool(HttpClient client)
{
    [QylCapability("trace_investigation", QylCapabilityRole.Starting)]
    [QylCapability("anomaly_detection", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "search_traces", Title = "Search Traces",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(
        "Search distributed traces by query. Returns a paginated list of matching traces with duration, status, and root span.")]
    public async Task<string> SearchTracesAsync(
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

        var url = $"/api/v1/mcp/traces?q={Uri.EscapeDataString(query)}&limit={limit}";
        if (projectSlug is not null)
            url += $"&project={Uri.EscapeDataString(projectSlug)}";
        if (cursor is not null)
            url += $"&cursor={Uri.EscapeDataString(cursor)}";

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
