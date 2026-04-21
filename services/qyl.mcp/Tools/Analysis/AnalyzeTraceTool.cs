using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using qyl.mcp.Formatting;
using qyl.mcp.Errors;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Analysis;

/// <summary>
///     MCP tool that analyzes a distributed trace returning span, error, service, and latency details.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Agent)]
public sealed class AnalyzeTraceTool(HttpClient client)
{
    /// <summary>
    ///     Retrieves and formats structured analysis of a distributed trace's spans, errors, and latency patterns.
    /// </summary>
    /// <param name="traceId">The trace ID to analyze.</param>
    /// <param name="focus">Optional focus area such as 'latency', 'errors', or 'dependencies'.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown string containing the trace analysis.</returns>
    [McpServerTool(
        Name = "analyze_trace",
        Title = "Analyze Trace",
        ReadOnly = false,
        Destructive = false,
        OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    [Description(
        "Analyze a distributed trace. Returns structured analysis of spans, errors, services, and latency patterns.")]
    public async Task<string> AnalyzeTraceAsync(
        [Description("Trace ID to analyze")] string traceId,
        [Description("What to focus on (e.g. 'latency', 'errors', 'dependencies')")]
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
