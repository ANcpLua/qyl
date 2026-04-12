
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;

namespace qyl.mcp.Tools.Errors;

/// <summary>
/// MCP tool that retrieves the breadcrumb trail for an error event showing actions leading up to the error.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class GetBreadcrumbsTool(HttpClient client)
{
    /// <summary>
    /// Retrieves the breadcrumb trail of user actions, HTTP requests, and navigation events for an error event.
    /// </summary>
    /// <param name="issueId">The error issue ID.</param>
    /// <param name="eventId">Optional specific event ID within the issue; uses the latest event if omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown list of breadcrumb entries in reverse chronological order.</returns>
    [QylCapability("error_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_breadcrumbs", Title = "Get Event Breadcrumbs",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get the breadcrumb trail for an error event — user actions, HTTP requests, console messages, and navigation events leading up to the error.")]
    public async Task<string> GetBreadcrumbsAsync(
        [Description("The error issue ID")]
        string issueId,
        [Description("Specific event ID within the issue (latest event if omitted)")]
        string? eventId = null,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/mcp/errors/{Uri.EscapeDataString(issueId)}/breadcrumbs";
        if (eventId is not null)
            url += $"?eventId={Uri.EscapeDataString(eventId)}";

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
