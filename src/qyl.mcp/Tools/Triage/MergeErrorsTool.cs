
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;

/// <summary>
/// Merges duplicate error issues into a primary issue, consolidating all events under the primary.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class MergeErrorsTool(HttpClient client)
{
    [McpServerTool(
        Name = "merge_errors",
        Title = "Merge Error Issues",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false)]
    [Description("Merge duplicate error issues into a primary issue. All events from the merged issues are consolidated under the primary.")]
    public async Task<string> MergeErrors(
        [Description("The primary issue ID that will absorb the duplicates")]
        string primaryIssueId,
        [Description("Issue IDs to merge into the primary issue")]
        List<string> issueIds,
        CancellationToken ct = default)
    {
        var body = new MergeErrorsRequestDto(primaryIssueId, issueIds);
        using var response = await client.PostAsJsonAsync(
                "/api/v1/mcp/errors/merge", body, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Error issue");

        response.EnsureSuccessStatusCode();
        return ResponseFormatter.FormatSuccess(
            $"Merged {issueIds.Count} issue(s) into primary issue `{primaryIssueId}`.");
    }
}

internal sealed record MergeErrorsRequestDto(
    string PrimaryIssueId,
    List<string> IssueIds);
