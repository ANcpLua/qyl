using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using qyl.mcp.Formatting;
using qyl.mcp.Errors;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Sessions;

/// <summary>
///     Updates the status of a debugging session (e.g. 'reviewed', 'investigating', 'resolved').
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class UpdateSessionStatusTool(HttpClient client)
{
    [McpServerTool(
        Name = "update_session_status",
        Title = "Update Session Status",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true)]
    [Description("Update the status of a debugging session (e.g. 'reviewed', 'investigating', 'resolved').")]
    public async Task<string> UpdateSessionStatus(
        [Description("Session ID")] string sessionId,
        [Description("New status (e.g. 'reviewed', 'investigating', 'resolved')")]
        string status,
        CancellationToken ct = default)
    {
        var body = new { status };
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/mcp/sessions/{Uri.EscapeDataString(sessionId)}/status") { Content = JsonContent.Create(body) };
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Session");

        response.EnsureSuccessStatusCode();
        return ResponseFormatter.FormatSuccess($"Session `{sessionId}` status updated to `{status}`.");
    }
}
