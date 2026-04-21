using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using qyl.mcp.Formatting;
using qyl.mcp.Errors;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Analysis;

/// <summary>
///     MCP tool that analyzes a session returning span count, status, service, and trace breakdown.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Agent)]
public sealed class AnalyzeSessionTool(HttpClient client)
{
    /// <summary>
    ///     Retrieves and formats structured analysis of a session's span count, status, service, and traces.
    /// </summary>
    /// <param name="sessionId">The session ID to analyze.</param>
    /// <param name="focus">Optional focus area such as 'latency', 'errors', or 'dependencies'.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown string containing the session analysis.</returns>
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
