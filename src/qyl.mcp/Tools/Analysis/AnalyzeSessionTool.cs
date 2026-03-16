using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Analysis;

[McpServerToolType]
public sealed class AnalyzeSessionTool(HttpClient client)
{
    [McpServerTool(
        Name = "analyze_session",
        Title = "Analyze Session",
        ReadOnly = false,
        Destructive = false,
        OpenWorld = true)]
    [Description("Analyze a session. Returns structured analysis of span count, status, service, and trace breakdown.")]
    public async Task<string> AnalyzeSessionAsync(
        [Description("Session ID to analyze")] string sessionId,
        [Description("What to focus on (e.g. 'latency', 'errors', 'dependencies')")]
        string? focus = null,
        CancellationToken ct = default)
    {
        var response = await client.GetAsync(
            $"/api/v1/mcp/sessions/{Uri.EscapeDataString(sessionId)}", ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Session");

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionDetailDto>(ct).ConfigureAwait(false);

        return ResponseFormatter.FormatDetail(
            $"Analysis of Session `{sessionId}`",
            [
                ("Span Count", session!.SpanCount.ToString()),
                ("Status", session.Status),
                ("Service", session.ServiceName),
                ("Trace Count", session.Traces?.Count.ToString() ?? "0"),
                ("Created", session.CreatedAt),
                ("Focus", focus ?? "general")
            ]);
    }
}
