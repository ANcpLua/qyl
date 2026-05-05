using System.Net;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class MarkTraceReviewedTool(HttpClient client)
{
    [McpServerTool(
        Name = "mark_trace_reviewed",
        Title = "Mark Trace Reviewed",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    public async partial Task<string> MarkTraceReviewed(
        string traceId,
        CancellationToken ct = default)
    {
        using var response = await client
            .PostAsync($"/api/v1/mcp/traces/{Uri.EscapeDataString(traceId)}/reviewed", null, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Trace");

        response.EnsureSuccessStatusCode();
        return ResponseFormatter.FormatSuccess($"Trace `{traceId}` marked as reviewed.");
    }
}
