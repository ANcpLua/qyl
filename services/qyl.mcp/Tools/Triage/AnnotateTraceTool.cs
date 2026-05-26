using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class AnnotateTraceTool(HttpClient client)
{
    [McpServerTool(
        Name = "annotate_trace",
        Title = "Annotate Trace",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true)]
    public async partial Task<string> AnnotateTrace(
        string traceId,
        string note,
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
