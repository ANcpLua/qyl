using System.ComponentModel;
using System.Net;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;

/// <summary>
/// Marks a trace as reviewed during triage.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
public sealed class MarkTraceReviewedTool(HttpClient client)
{
    [McpServerTool(
        Name = "mark_trace_reviewed",
        Title = "Mark Trace Reviewed",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Mark a trace as reviewed during triage.")]
    /// <summary>
    /// Posts a reviewed marker to the specified trace for triage tracking.
    /// </summary>
    /// <param name="traceId">The trace ID to mark as reviewed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success confirmation message.</returns>
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
