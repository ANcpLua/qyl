
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;

namespace qyl.mcp.Tools.Errors;

/// <summary>
/// MCP tool that retrieves the distribution of a tag's values for an error issue.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class GetTagDistributionTool(HttpClient client)
{
    /// <summary>
    /// Retrieves occurrence counts and percentages for each value of a specific tag on an error issue.
    /// </summary>
    /// <param name="issueId">The error issue ID.</param>
    /// <param name="tagKey">The tag key to inspect (e.g. "browser", "environment", "url", "os").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown table with value, count, percentage, and bar chart columns.</returns>
    [QylCapability("error_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_tag_distribution", Title = "Get Tag Distribution",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
                 Get the distribution of a specific tag's values for an error issue.

                 Shows which tag values (browsers, environments, URLs, OS versions, etc.)
                 are most affected, with occurrence counts and percentages.

                 Common tag keys: "browser", "environment", "url", "os", "device", "runtime", "server_name"

                 Returns: Markdown table with value, count, and percentage columns
                 """)]
    public async Task<string> GetTagDistributionAsync(
        [Description("The error issue ID")]
        string issueId,
        [Description("The tag key to inspect (e.g. \"browser\", \"environment\", \"url\", \"os\")")]
        string tagKey,
        CancellationToken ct = default)
    {
        var response = await client.GetAsync(
            $"/api/v1/mcp/errors/{Uri.EscapeDataString(issueId)}/tags/{Uri.EscapeDataString(tagKey)}",
            ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Error issue");

        response.EnsureSuccessStatusCode();

        var distribution = await response.Content
            .ReadFromJsonAsync<TagDistributionResponse>(ct).ConfigureAwait(false);

        if (distribution is null || distribution.Values is { Count: 0 })
            return $"No tag data for `{tagKey}` on issue `{issueId}`.";

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"# Tag Distribution: `{distribution.TagKey}` ({distribution.TotalCount:N0} total)");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Issue `{issueId}`");
        sb.AppendLine();
        sb.AppendLine("| Value | Count | % | Bar |");
        sb.AppendLine("|-------|------:|--:|-----|");

        foreach (var tag in distribution.Values)
        {
            var barLength = (int)(tag.Percentage / 100.0 * 20);
            var bar = new string('\u2588', barLength) + new string('\u2591', 20 - barLength);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {tag.Value} | {tag.Count:N0} | {tag.Percentage:F1}% | `{bar}` |");
        }

        sb.AppendLine();
        sb.AppendLine("## Next steps");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- Use `get_error_issue(issueId: '{issueId}')` for full issue details");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- Use `get_breadcrumbs(issueId: '{issueId}')` for the event breadcrumb trail");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- Use `get_error_timeline(issueId: '{issueId}')` to see occurrence trends");

        return sb.ToString();
    }
}

internal sealed record TagValueDto(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("count")] long Count,
    [property: JsonPropertyName("percentage")]
    double Percentage);

internal sealed record TagDistributionResponse(
    [property: JsonPropertyName("tag_key")]
    string TagKey,
    [property: JsonPropertyName("total_count")]
    long TotalCount,
    [property: JsonPropertyName("values")] IReadOnlyList<TagValueDto> Values);
