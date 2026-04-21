using System.ComponentModel;
using System.Net;
using qyl.mcp.Formatting;
using qyl.mcp.Errors;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Triage;

/// <summary>
///     Marks a trace as reviewed during triage.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class MarkTraceReviewedTool(HttpClient client)
{
    [McpServerTool(
        Name = "mark_trace_reviewed",
        Title = "Mark Trace Reviewed",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Mark a trace as reviewed during triage.")]
    public async Task<string> MarkTraceReviewed(
        [Description("Trace ID to mark as reviewed")]
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
