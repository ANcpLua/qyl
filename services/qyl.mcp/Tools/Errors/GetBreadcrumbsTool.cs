using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;

namespace qyl.mcp.Tools.Errors;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class GetBreadcrumbsTool(HttpClient client)
{
    [QylCapability("error_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_breadcrumbs", Title = "Get Event Breadcrumbs",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public async partial Task<string> GetBreadcrumbsAsync(
        string issueId,
        string? eventId = null,
        CancellationToken ct = default)
    {
        var url = QueryString.AppendPairs(
            $"/api/v1/mcp/errors/{Uri.EscapeDataString(issueId)}/breadcrumbs",
            ("eventId", eventId));

        var response = await client.GetAsync(url, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Error issue");

        response.EnsureSuccessStatusCode();

        var breadcrumbs = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<BreadcrumbDto>>(ct).ConfigureAwait(false);

        if (breadcrumbs is null or { Count: 0 })
            return "No breadcrumbs recorded for this event.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Breadcrumbs ({breadcrumbs.Count} entries)");
        sb.AppendLine();

        var eventSuffix = eventId is not null ? $" (event `{eventId}`)" : " (latest event)";
        sb.AppendLine($"Issue `{issueId}`{eventSuffix}");
        sb.AppendLine();

        foreach (var crumb in breadcrumbs.Reverse())
        {
            var levelBadge = crumb.Level switch
            {
                "error" or "fatal" => " **[ERROR]**",
                "warning" => " *[WARN]*",
                _ => ""
            };

            sb.AppendLine($"- `{crumb.Timestamp}` **{crumb.Category}**{levelBadge} {crumb.Message}");

            if (crumb.Data is { Count: > 0 })
            {
                foreach (var (key, value) in crumb.Data)
                    sb.AppendLine($"  - `{key}`: {value}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Next steps");
        sb.AppendLine($"- Use `get_error_issue(issueId: '{issueId}')` for full issue details");
        sb.AppendLine($"- Use `get_error_timeline(issueId: '{issueId}')` to see occurrence trends");

        return sb.ToString();
    }
}

internal sealed record BreadcrumbDto(
    [property: JsonPropertyName("timestamp")]
    string Timestamp,
    [property: JsonPropertyName("category")]
    string Category,
    [property: JsonPropertyName("message")]
    string Message,
    [property: JsonPropertyName("level")] string? Level = null,
    [property: JsonPropertyName("data")] Dictionary<string, string>? Data = null);
