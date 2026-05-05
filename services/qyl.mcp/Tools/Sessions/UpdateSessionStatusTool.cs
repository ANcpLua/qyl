using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Sessions;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class UpdateSessionStatusTool(HttpClient client)
{
    [McpServerTool(
        Name = "update_session_status",
        Title = "Update Session Status",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true)]
    public async partial Task<string> UpdateSessionStatus(
        string sessionId,
        string status,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/mcp/sessions/{Uri.EscapeDataString(sessionId)}/status");
        request.Content = JsonContent.Create(new { status });
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Session");

        response.EnsureSuccessStatusCode();
        return ResponseFormatter.FormatSuccess($"Session `{sessionId}` status updated to `{status}`.");
    }
}
