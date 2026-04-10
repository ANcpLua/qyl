using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Sessions;

/// <summary>
/// Adds an annotation with optional tags to a debugging session.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
public sealed class AnnotateSessionTool(HttpClient client)
{
    [McpServerTool(
        Name = "annotate_session",
        Title = "Annotate Session",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Add an annotation with optional tags to a debugging session.")]
    /// <summary>
    /// Posts an annotation note with optional tags to the specified debugging session.
    /// </summary>
    /// <param name="sessionId">The session ID to annotate.</param>
    /// <param name="note">The annotation text.</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success confirmation message.</returns>
    public async Task<string> AnnotateSession(
        [Description("Session ID to annotate")]
        string sessionId,
        [Description("Annotation note")] string note,
        [Description("Optional tags for categorization")]
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
