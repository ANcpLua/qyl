using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Sessions;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class AnnotateSessionTool(HttpClient client)
{
    [McpServerTool(
        Name = "annotate_session",
        Title = "Annotate Session",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    public async partial Task<string> AnnotateSession(
        string sessionId,
        string note,
        List<string>? tags = null,
        CancellationToken ct = default)
    {
        var body = new AnnotationRequestDto(note, tags);
        using var response = await client.PostAsJsonAsync(
                $"/api/v1/mcp/sessions/{Uri.EscapeDataString(sessionId)}/annotations", body, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Session");

        response.EnsureSuccessStatusCode();
        return ResponseFormatter.FormatSuccess($"Annotation added to session `{sessionId}`.");
    }
}
