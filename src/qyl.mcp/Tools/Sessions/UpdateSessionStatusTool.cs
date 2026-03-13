using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Sessions;

[McpServerToolType]
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
        [Description("New status (e.g. 'reviewed', 'investigating', 'resolved')")] string status,
        CancellationToken ct = default)
    {
        var body = new { status };
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/mcp/sessions/{Uri.EscapeDataString(sessionId)}/status")
        {
            Content = JsonContent.Create(body)
        };
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.NotFound)
            throw new QylNotFoundException("Session");

        response.EnsureSuccessStatusCode();
        return ResponseFormatter.FormatSuccess($"Session `{sessionId}` status updated to `{status}`.");
    }
}
