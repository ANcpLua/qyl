using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class LinkErrorsTool(HttpClient client)
{
    [McpServerTool(
        Name = "link_errors",
        Title = "Link Error Issues",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    public async partial Task<string> LinkErrors(
        string issueId,
        string linkedIssueId,
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
