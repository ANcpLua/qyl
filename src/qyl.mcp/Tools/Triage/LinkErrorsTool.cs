
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;

/// <summary>
/// Links two related error issues together for cross-referencing without merging.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class LinkErrorsTool(HttpClient client)
{
    [McpServerTool(
        Name = "link_errors",
        Title = "Link Error Issues",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Link two related error issues together for cross-referencing without merging. Relationship types: causes, caused_by, related.")]
    /// <summary>
    /// Creates a relationship link between two error issues (causes, caused_by, or related).
    /// </summary>
    /// <param name="issueId">The source error issue ID.</param>
    /// <param name="linkedIssueId">The target error issue ID to link.</param>
    /// <param name="relationship">Relationship type: causes, caused_by, or related.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success confirmation message with the link details.</returns>
    public async Task<string> LinkErrors(
        [Description("Source error issue ID")] string issueId,
        [Description("Target error issue ID to link")] string linkedIssueId,
        [Description("Relationship type: causes, caused_by, or related")]
        string relationship = "related",
        CancellationToken ct = default)
    {
        var body = new LinkErrorsRequestDto(linkedIssueId, relationship);
        using var response = await client.PostAsJsonAsync(
                $"/api/v1/mcp/errors/{Uri.EscapeDataString(issueId)}/links", body, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Error issue");

        response.EnsureSuccessStatusCode();
        return ResponseFormatter.FormatSuccess(
            $"Linked error issue `{issueId}` -> `{linkedIssueId}` ({relationship}).");
    }
}

internal sealed record LinkErrorsRequestDto(
    [property: JsonPropertyName("linkedIssueId")]
    string LinkedIssueId,
    [property: JsonPropertyName("relationship")]
    string Relationship);
