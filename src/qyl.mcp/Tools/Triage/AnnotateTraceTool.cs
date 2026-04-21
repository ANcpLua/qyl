using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using qyl.mcp.Formatting;
using qyl.mcp.Errors;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Triage;

/// <summary>
///     Adds an annotation with optional tags to a trace for triage purposes.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class AnnotateTraceTool(HttpClient client)
{
    [McpServerTool(
        Name = "annotate_trace",
        Title = "Annotate Trace",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Add an annotation with optional tags to a trace for triage purposes.")]
    public async Task<string> AnnotateTrace(
        [Description("Trace ID to annotate")] string traceId,
        [Description("Annotation note")] string note,
        [Description("Optional tags for categorization")]
        List<string>? tags = null,
        CancellationToken ct = default)
    {
        var body = new AnnotationRequestDto(note, tags);
        using var response = await client.PostAsJsonAsync(
                $"/api/v1/mcp/traces/{Uri.EscapeDataString(traceId)}/annotations", body, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Trace");

        response.EnsureSuccessStatusCode();
        return ResponseFormatter.FormatSuccess($"Annotation added to trace `{traceId}`.");
    }
}
