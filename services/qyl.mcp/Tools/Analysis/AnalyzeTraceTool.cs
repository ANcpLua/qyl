using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Analysis;

[McpServerToolType]
[QylSkill(QylSkillKind.Agent)]
public sealed partial class AnalyzeTraceTool(HttpClient client)
{
    [McpServerTool(
        Name = "analyze_trace",
        Title = "Analyze Trace",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> AnalyzeTraceAsync(
        string traceId,
        string? focus = null,
        CancellationToken ct = default)
    {
        var response = await client.GetAsync(
            $"/api/v1/mcp/traces/{Uri.EscapeDataString(traceId)}", ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Trace");

        response.EnsureSuccessStatusCode();
        var spans = await response.Content.ReadFromJsonAsync<IReadOnlyList<SpanDetailDto>>(ct).ConfigureAwait(false);

        var spanList = spans ?? [];
        var errorSpans = spanList.Where(s => s.Status is "error").ToList();
        var slowestSpan = spanList.OrderByDescending(s => s.DurationMs).FirstOrDefault();

        return ResponseFormatter.FormatDetail(
            $"Analysis of Trace `{traceId}`",
            [
                ("Total Spans", spanList.Count.ToString()),
                ("Error Spans", errorSpans.Count.ToString()),
                ("Services", string.Join(", ", spanList.Select(s => s.ServiceName).Distinct())),
                ("Total Duration", $"{spanList.Max(s => s.DurationMs):N0}ms"),
                ("Slowest Span", slowestSpan?.SpanName ?? "N/A"),
                ("Focus", focus ?? "general")
            ]);
    }
}
