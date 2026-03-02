using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Sentry;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for Sentry issue management.
///     Covers: search_issues, get_issue, update_issue (triage).
/// </summary>
[McpServerToolType]
public sealed class qylIssueTools(HttpClient client)
{
    // ── Search ────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_search_issues")]
    [Description("""
                 Search Sentry issues in a project.

                 Supports Sentry's full query syntax:
                 - is:unresolved — unresolved only (default)
                 - is:resolved — resolved issues
                 - level:error — only errors
                 - assigned:me — assigned to current user
                 - has:stacktrace — has a stack trace
                 - times_seen:>100 — seen more than 100 times

                 Example queries:
                 - Recent errors: sentry_search_issues(org, project)
                 - By text: sentry_search_issues(org, project, query="NullReferenceException")
                 - Unresolved errors: sentry_search_issues(org, project, query="is:unresolved level:error")

                 Returns: Issue list with titles, counts, and last-seen timestamps
                 """)]
    public Task<string> SearchIssuesAsync(
        [Description("Organization slug")] string orgSlug,
        [Description("Project slug")] string projectSlug,
        [Description("Sentry search query (default: is:unresolved)")]
        string query = "is:unresolved",
        [Description("Maximum issues to return (default: 25)")]
        int limit = 25) =>
        qylHelper.ExecuteAsync(async () =>
        {
            var url = $"projects/{Uri.EscapeDataString(orgSlug)}/{Uri.EscapeDataString(projectSlug)}/issues/"
                      + $"?query={Uri.EscapeDataString(query)}&limit={limit}";

            var issues = await client.GetFromJsonAsync<SentryIssueDto[]>(
                url, SentryIssueJsonContext.Default.SentryIssueDtoArray).ConfigureAwait(false);

            if (issues is null || issues.Length is 0)
                return $"No issues found in {orgSlug}/{projectSlug} for query '{query}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Issues in {orgSlug}/{projectSlug} ({issues.Length})");
            sb.AppendLine($"Query: `{query}`");
            sb.AppendLine();

            foreach (var issue in issues)
            {
                var statusBadge = issue.Status == "resolved" ? "✅" : "🔴";
                sb.AppendLine($"## {statusBadge} [{issue.ShortId}] {issue.Title}");
                sb.AppendLine($"- **Seen:** {issue.Count}× — last {issue.LastSeen:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"- **Users affected:** {issue.UserCount}");
                if (!string.IsNullOrEmpty(issue.Culprit))
                    sb.AppendLine($"- **Culprit:** {issue.Culprit}");
                sb.AppendLine($"- **ID:** `{issue.Id}`");
                sb.AppendLine();
            }

            return sb.ToString();
        });

    // ── Get ───────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_get_issue")]
    [Description("""
                 Get full details for a single Sentry issue.

                 Returns metadata, tags, and the latest event's stack trace.
                 The issue_id can be the numeric ID or short ID (e.g., PROJECT-123).

                 Use sentry_search_issues to find issue IDs.

                 Returns: Issue details with stack trace and tag breakdown
                 """)]
    public Task<string> GetIssueAsync(
        [Description("Issue ID or short ID (e.g., 'PROJECT-123' or '123456789')")]
        string issueId) =>
        qylHelper.ExecuteAsync(async () =>
        {
            var issue = await client.GetFromJsonAsync<SentryIssueDetailDto>(
                $"issues/{Uri.EscapeDataString(issueId)}/",
                SentryIssueJsonContext.Default.SentryIssueDetailDto).ConfigureAwait(false);

            if (issue is null) return $"Issue '{issueId}' not found.";

            var sb = new StringBuilder();
            sb.AppendLine($"# [{issue.ShortId}] {issue.Title}");
            sb.AppendLine();
            sb.AppendLine($"- **Status:** {issue.Status}");
            sb.AppendLine($"- **Level:** {issue.Level}");
            sb.AppendLine($"- **Times seen:** {issue.Count}");
            sb.AppendLine($"- **Users affected:** {issue.UserCount}");
            sb.AppendLine($"- **First seen:** {issue.FirstSeen:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"- **Last seen:** {issue.LastSeen:yyyy-MM-dd HH:mm}");

            if (!string.IsNullOrEmpty(issue.Culprit))
                sb.AppendLine($"- **Culprit:** {issue.Culprit}");

            if (!string.IsNullOrEmpty(issue.AssignedTo))
                sb.AppendLine($"- **Assigned to:** {issue.AssignedTo}");

            if (issue.Tags is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine("## Top Tags");
                foreach (var tag in issue.Tags.Take(10))
                    sb.AppendLine($"- **{tag.Key}:** {string.Join(", ", tag.TopValues?.Select(v => $"{v.Value} ({v.Count}×)") ?? [])}");
            }

            if (!string.IsNullOrEmpty(issue.Permalink))
            {
                sb.AppendLine();
                sb.AppendLine($"🔗 {issue.Permalink}");
            }

            return sb.ToString();
        });

    // ── Update / Triage ───────────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_update_issue")]
    [Description("""
                 Update a Sentry issue — resolve, ignore, or assign.

                 Actions:
                 - status="resolved" — mark as resolved
                 - status="ignored" — snooze/ignore the issue
                 - status="unresolved" — reopen
                 - assignedTo="username" or "team:team-slug" — assign

                 Requires token with event:write scope.

                 Returns: Updated issue status confirmation
                 """)]
    public Task<string> UpdateIssueAsync(
        [Description("Issue ID or short ID")] string issueId,
        [Description("New status: 'resolved', 'ignored', or 'unresolved'")]
        string? status = null,
        [Description("Assign to user ('username') or team ('team:slug')")]
        string? assignedTo = null) =>
        qylHelper.ExecuteAsync(async () =>
        {
            if (status is null && assignedTo is null)
                return "Provide at least one field to update: status or assignedTo.";

            var payload = new Dictionary<string, string>();
            if (status is not null) payload["status"] = status;
            if (assignedTo is not null) payload["assignedTo"] = assignedTo;

            using var response = await client.PutAsJsonAsync(
                $"issues/{Uri.EscapeDataString(issueId)}/",
                payload).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return $"Failed to update issue '{issueId}': HTTP {(int)response.StatusCode}";

            var sb = new StringBuilder();
            sb.AppendLine($"# Issue Updated: {issueId}");
            if (status is not null) sb.AppendLine($"- Status → **{status}**");
            if (assignedTo is not null) sb.AppendLine($"- Assigned to → **{assignedTo}**");
            return sb.ToString();
        });
}

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

internal sealed record SentryIssueDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("shortId")] string ShortId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("culprit")] string? Culprit,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("count")] string Count,
    [property: JsonPropertyName("userCount")] int UserCount,
    [property: JsonPropertyName("firstSeen")] DateTimeOffset FirstSeen,
    [property: JsonPropertyName("lastSeen")] DateTimeOffset LastSeen);

internal sealed record SentryIssueDetailDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("shortId")] string ShortId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("culprit")] string? Culprit,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("level")] string? Level,
    [property: JsonPropertyName("count")] string Count,
    [property: JsonPropertyName("userCount")] int UserCount,
    [property: JsonPropertyName("firstSeen")] DateTimeOffset FirstSeen,
    [property: JsonPropertyName("lastSeen")] DateTimeOffset LastSeen,
    [property: JsonPropertyName("assignedTo")] string? AssignedTo,
    [property: JsonPropertyName("permalink")] string? Permalink,
    [property: JsonPropertyName("tags")] List<SentryTagDto>? Tags);

internal sealed record SentryTagDto(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("topValues")] List<SentryTagValueDto>? TopValues);

internal sealed record SentryTagValueDto(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("count")] int Count);

// ─────────────────────────────────────────────────────────────────────────────
// JSON context (AOT)
// ─────────────────────────────────────────────────────────────────────────────

[JsonSerializable(typeof(SentryIssueDto[]))]
[JsonSerializable(typeof(SentryIssueDetailDto))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class SentryIssueJsonContext : JsonSerializerContext;
