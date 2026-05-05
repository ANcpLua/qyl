using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class MergeErrorsTool(HttpClient client)
{
    [McpServerTool(
        Name = "merge_errors",
        Title = "Merge Error Issues",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false)]
    public async partial Task<string> MergeErrors(
        string primaryIssueId,
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
