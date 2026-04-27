using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Sessions;

/// <summary>
///     Retrieves full details of a debugging session including associated traces.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class GetSessionTool(HttpClient client)
{
    [McpServerTool(
        Name = "get_session",
        Title = "Get Session",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false)]
    public async partial Task<string> GetSession(
        string sessionId,
        CancellationToken ct = default)
    {
        using var response = await client
            .GetAsync($"/api/v1/mcp/sessions/{Uri.EscapeDataString(sessionId)}", ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Session");

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionDetailDto>(ct).ConfigureAwait(false);

        return ResponseFormatter.FormatDetail(
            $"Session `{session!.SessionId}`",
            [
                ("Status", session.Status),
                ("Service", session.ServiceName),
                ("Span Count", session.SpanCount.ToString()),
                ("Created", session.CreatedAt),
                ("Traces", session.Traces is { Count: > 0 }
                    ? string.Join("\n",
                        session.Traces.Select(t =>
                            $"  - `{t.TraceId}` | {t.Status} | {t.DurationMs:N0}ms | {t.Service}"))
                    : "none")
            ]);
    }
}
