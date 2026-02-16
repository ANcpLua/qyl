using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying grouped error issues and triggering fixes.
/// </summary>
[McpServerToolType]
public sealed class IssueTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_issues")]
    [Description("""
                 List grouped errors with status, owner, and event count.

                 Shows error issues aggregated by fingerprint:
                 - Issue title and status
                 - Assigned owner
                 - Event count and first/last seen
                 - Severity level

                 Example queries:
                 - All open: list_issues(status="open")
                 - Recent 5: list_issues(limit=5)

                 Returns: Table of grouped error issues
                 """)]
    public Task<string> ListIssuesAsync(
        [Description("Maximum issues to return (default: 20)")]
        int limit = 20,
        [Description("Filter by status: 'open', 'resolved', 'ignored'")]
        string? status = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var url = $"/api/v1/issues?limit={limit}";
            if (!string.IsNullOrEmpty(status))
                url += $"&status={Uri.EscapeDataString(status)}";

            var response = await client.GetFromJsonAsync<IssuesListResponse>(
                url, IssueJsonContext.Default.IssuesListResponse).ConfigureAwait(false);

            if (response?.Issues is null || response.Issues.Count is 0)
                return "No issues found matching the criteria.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Issues ({response.Issues.Count} results)");
            sb.AppendLine();
            sb.AppendLine("| ID | Title | Status | Owner | Events | First Seen | Last Seen |");
            sb.AppendLine("|----|-------|--------|-------|--------|------------|-----------|");

            foreach (var issue in response.Issues)
            {
                var statusIcon = issue.Status switch
                {
                    "open" => "🔴",
                    "resolved" => "✅",
                    "ignored" => "⏸️",
                    _ => "❓"
                };
                var id = issue.IssueId.Length > 8 ? issue.IssueId[..8] : issue.IssueId;
                var owner = issue.Owner ?? "-";
                var firstSeen = issue.FirstSeen ?? "-";
                var lastSeen = issue.LastSeen ?? "-";

                sb.AppendLine(
                    $"| {id} | {issue.Title ?? "untitled"} | {statusIcon} {issue.Status} | {owner} | {issue.EventCount:N0} | {firstSeen} | {lastSeen} |");
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.get_issue")]
    [Description("""
                 Get detailed info for a specific issue with its events.

                 Returns full details including:
                 - Issue title and description
                 - Status and owner
                 - Event count and timeline
                 - Recent events with stack traces

                 Returns: Full issue details with events
                 """)]
    public Task<string> GetIssueAsync(
        [Description("The issue ID to look up")]
        string issueId) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var issue = await client.GetFromJsonAsync<IssueDetailDto>(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}",
                IssueJsonContext.Default.IssueDetailDto).ConfigureAwait(false);

            if (issue is null)
                return $"Issue '{issueId}' not found.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Issue: {issue.Title ?? "untitled"}");
            sb.AppendLine();
            sb.AppendLine($"- **ID:** {issue.IssueId}");
            sb.AppendLine($"- **Status:** {issue.Status}");
            sb.AppendLine($"- **Owner:** {issue.Owner ?? "unassigned"}");
            sb.AppendLine($"- **Events:** {issue.EventCount:N0}");

            if (!string.IsNullOrEmpty(issue.FirstSeen))
                sb.AppendLine($"- **First seen:** {issue.FirstSeen}");

            if (!string.IsNullOrEmpty(issue.LastSeen))
                sb.AppendLine($"- **Last seen:** {issue.LastSeen}");

            if (!string.IsNullOrEmpty(issue.Description))
            {
                sb.AppendLine();
                sb.AppendLine("## Description");
                sb.AppendLine(issue.Description);
            }

            if (issue.Events is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine($"## Recent Events ({issue.Events.Count})");
                sb.AppendLine();
                sb.AppendLine("| Time | Trace ID | Message |");
                sb.AppendLine("|------|----------|---------|");

                foreach (var evt in issue.Events)
                {
                    var traceId = evt.TraceId is { Length: > 8 } ? evt.TraceId[..8] : evt.TraceId ?? "-";
                    sb.AppendLine($"| {evt.Timestamp ?? "-"} | {traceId} | {evt.Message ?? "-"} |");
                }
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.trigger_fix")]
    [Description("""
                 Dispatch an autofix workflow run for a specific issue.

                 Triggers a fix attempt using the specified policy:
                 - 'auto': Automatically apply the most likely fix
                 - 'suggest': Generate fix suggestions without applying
                 - 'review': Create a review with fix recommendations

                 Returns: Fix dispatch confirmation with run ID
                 """)]
    public Task<string> TriggerFixAsync(
        [Description("The issue ID to fix")] string issueId,
        [Description("Fix policy: 'auto', 'suggest', or 'review' (default: 'suggest')")]
        string policy = "suggest") =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var request = new TriggerFixRequest(issueId, policy);
            var response = await client.PostAsJsonAsync(
                "/api/v1/issues/fix",
                request,
                IssueJsonContext.Default.TriggerFixRequest).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return
                    $"Failed to trigger fix (HTTP {(int)response.StatusCode}). The issue may not exist or the policy is invalid.";

            var result = await response.Content.ReadFromJsonAsync(
                IssueJsonContext.Default.TriggerFixResponse).ConfigureAwait(false);

            var sb = new StringBuilder();
            sb.AppendLine("# Fix Dispatched");
            sb.AppendLine();
            sb.AppendLine($"- **Issue:** {issueId}");
            sb.AppendLine($"- **Policy:** {policy}");

            if (result is not null)
            {
                if (!string.IsNullOrEmpty(result.RunId))
                    sb.AppendLine($"- **Run ID:** {result.RunId}");
                if (!string.IsNullOrEmpty(result.Status))
                    sb.AppendLine($"- **Status:** {result.Status}");
            }

            return sb.ToString();
        });
}

#region DTOs

internal sealed record IssuesListResponse(
    [property: JsonPropertyName("issues")] List<IssueSummaryDto>? Issues,
    [property: JsonPropertyName("total")] int Total);

internal sealed record IssueSummaryDto(
    [property: JsonPropertyName("issue_id")]
    string IssueId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("event_count")]
    long EventCount,
    [property: JsonPropertyName("first_seen")]
    string? FirstSeen,
    [property: JsonPropertyName("last_seen")]
    string? LastSeen);

internal sealed record IssueDetailDto(
    [property: JsonPropertyName("issue_id")]
    string IssueId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")]
    string? Description,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("event_count")]
    long EventCount,
    [property: JsonPropertyName("first_seen")]
    string? FirstSeen,
    [property: JsonPropertyName("last_seen")]
    string? LastSeen,
    [property: JsonPropertyName("events")] List<IssueEventDto>? Events);

internal sealed record IssueEventDto(
    [property: JsonPropertyName("timestamp")]
    string? Timestamp,
    [property: JsonPropertyName("trace_id")]
    string? TraceId,
    [property: JsonPropertyName("message")]
    string? Message);

internal sealed record TriggerFixRequest(
    [property: JsonPropertyName("issue_id")]
    string IssueId,
    [property: JsonPropertyName("policy")] string Policy);

internal sealed record TriggerFixResponse(
    [property: JsonPropertyName("run_id")] string? RunId,
    [property: JsonPropertyName("status")] string? Status);

#endregion

[JsonSerializable(typeof(IssuesListResponse))]
[JsonSerializable(typeof(IssueDetailDto))]
[JsonSerializable(typeof(TriggerFixRequest))]
[JsonSerializable(typeof(TriggerFixResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class IssueJsonContext : JsonSerializerContext;
