using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying the error issue system.
///     Provides fingerprinted error groups, event details, similarity search, and timeline analysis.
/// </summary>
[McpServerToolType]
public sealed class ErrorTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_error_issues", Title = "List Error Issues",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 List fingerprinted error groups (issues) with optional filtering.

                 Shows grouped errors with:
                 - Title and error type
                 - Status (unresolved, acknowledged, investigating, resolved, etc.)
                 - Priority and occurrence count
                 - First/last seen timestamps

                 Example queries:
                 - All unresolved: list_error_issues(status="unresolved")
                 - High priority: list_error_issues(priority="high")
                 - Recent errors: list_error_issues(limit=10)

                 Returns: List of error issues with details
                 """)]
    public Task<string> ListErrorIssuesAsync(
        [Description("Filter by status: 'unresolved', 'acknowledged', 'investigating', 'resolved', 'ignored'")] string? status = null,
        [Description("Filter by priority: 'critical', 'high', 'medium', 'low'")] string? priority = null,
        [Description("Filter by level: 'error', 'warning', 'fatal'")] string? level = null,
        [Description("Maximum issues to return (default: 50)")] int limit = 50) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            string url = $"/api/v1/issues?limit={limit}";
            if (!string.IsNullOrEmpty(status))
                url += $"&status={Uri.EscapeDataString(status)}";
            if (!string.IsNullOrEmpty(priority))
                url += $"&priority={Uri.EscapeDataString(priority)}";
            if (!string.IsNullOrEmpty(level))
                url += $"&level={Uri.EscapeDataString(level)}";

            ErrorIssueListResponse? response = await client.GetFromJsonAsync<ErrorIssueListResponse>(
                url, ErrorJsonContext.Default.ErrorIssueListResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count is 0)
                return "No error issues found matching the criteria.";

            StringBuilder sb = new();
            sb.AppendLine($"# Error Issues ({response.Items.Count} of {response.Total})");
            sb.AppendLine();
            sb.AppendLine("| Status | Priority | Title | Type | Occurrences | Last Seen |");
            sb.AppendLine("|--------|----------|-------|------|-------------|-----------|");

            foreach (ErrorIssueDto issue in response.Items)
            {
                sb.AppendLine(
                    $"| {issue.Status} | {issue.Priority} | {issue.Title} | {issue.ErrorType} | {issue.OccurrenceCount:N0} | {issue.LastSeenAt:yyyy-MM-dd HH:mm} |");
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.get_error_issue", Title = "Get Error Issue",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Get detailed information about a specific error issue, optionally with recent events.

                 Shows:
                 - Issue metadata (title, type, category, level, status, priority)
                 - Occurrence and affected user counts
                 - First/last seen timestamps
                 - Culprit and assignee
                 - Recent event list with stack traces (if includeEvents is true)

                 Example queries:
                 - Full details: get_error_issue(issueId="abc123")
                 - Without events: get_error_issue(issueId="abc123", includeEvents=false)

                 Returns: Detailed error issue with optional recent events
                 """)]
    public Task<string> GetErrorIssueAsync(
        [Description("The issue ID to retrieve")] string issueId,
        [Description("Include recent events with stack traces (default: true)")] bool includeEvents = true,
        [Description("Maximum events to return (default: 10)")] int eventLimit = 10) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            ErrorIssueDto? issue = await client.GetFromJsonAsync<ErrorIssueDto>(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}",
                ErrorJsonContext.Default.ErrorIssueDto).ConfigureAwait(false);

            if (issue is null)
                return $"Error issue '{issueId}' not found.";

            StringBuilder sb = new();
            sb.AppendLine($"# {issue.Title}");
            sb.AppendLine();
            sb.AppendLine($"- **ID:** {issue.Id}");
            sb.AppendLine($"- **Type:** {issue.ErrorType}");
            sb.AppendLine($"- **Category:** {issue.Category}");
            sb.AppendLine($"- **Level:** {issue.Level}");
            sb.AppendLine($"- **Status:** {issue.Status}");
            sb.AppendLine($"- **Priority:** {issue.Priority}");
            sb.AppendLine($"- **Occurrences:** {issue.OccurrenceCount:N0}");
            sb.AppendLine($"- **Affected Users:** {issue.AffectedUsersCount:N0}");
            sb.AppendLine($"- **First Seen:** {issue.FirstSeenAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- **Last Seen:** {issue.LastSeenAt:yyyy-MM-dd HH:mm:ss}");

            if (!string.IsNullOrEmpty(issue.Culprit))
                sb.AppendLine($"- **Culprit:** {issue.Culprit}");
            if (!string.IsNullOrEmpty(issue.AssignedTo))
                sb.AppendLine($"- **Assigned To:** {issue.AssignedTo}");

            sb.AppendLine($"- **Fingerprint:** {issue.Fingerprint}");

            if (includeEvents)
            {
                ErrorIssueEventsResponse? eventsResponse = await client.GetFromJsonAsync<ErrorIssueEventsResponse>(
                    $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/events?limit={eventLimit}",
                    ErrorJsonContext.Default.ErrorIssueEventsResponse).ConfigureAwait(false);

                if (eventsResponse?.Items is { Count: > 0 })
                {
                    sb.AppendLine();
                    sb.AppendLine($"## Recent Events ({eventsResponse.Items.Count} of {eventsResponse.Total})");
                    sb.AppendLine();

                    foreach (ErrorIssueEventDto evt in eventsResponse.Items)
                    {
                        sb.AppendLine($"### Event {evt.Id} — {evt.Timestamp:yyyy-MM-dd HH:mm:ss}");
                        if (!string.IsNullOrEmpty(evt.TraceId))
                            sb.AppendLine($"- Trace: {evt.TraceId}");
                        if (!string.IsNullOrEmpty(evt.SpanId))
                            sb.AppendLine($"- Span: {evt.SpanId}");
                        if (!string.IsNullOrEmpty(evt.Environment))
                            sb.AppendLine($"- Environment: {evt.Environment}");
                        if (!string.IsNullOrEmpty(evt.Message))
                            sb.AppendLine($"- Message: {evt.Message}");
                        if (!string.IsNullOrEmpty(evt.StackTrace))
                        {
                            sb.AppendLine("- Stack Trace:");
                            sb.AppendLine("```");
                            sb.AppendLine(evt.StackTrace);
                            sb.AppendLine("```");
                        }

                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.find_similar_errors", Title = "Find Similar Errors",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Find errors similar to a given span using embedding clusters.

                 Uses vector similarity to find spans with similar error patterns.
                 Useful for identifying related issues across different services.

                 Example queries:
                 - Find similar: find_similar_errors(spanId="abc123")
                 - Limit results: find_similar_errors(spanId="abc123", limit=5)

                 Returns: List of similar spans with cluster labels and similarity scores
                 """)]
    public Task<string> FindSimilarErrorsAsync(
        [Description("The span ID to find similar errors for")] string spanId,
        [Description("Maximum similar spans to return (default: 10)")] int limit = 10) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            SimilarSpansResponse? response = await client.GetFromJsonAsync<SimilarSpansResponse>(
                $"/api/v1/issues/similar?spanId={Uri.EscapeDataString(spanId)}&limit={limit}",
                ErrorJsonContext.Default.SimilarSpansResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count is 0)
                return $"No similar errors found for span '{spanId}'.";

            StringBuilder sb = new();
            sb.AppendLine($"# Similar Errors for Span {spanId}");
            sb.AppendLine();

            foreach (SimilarSpanDto span in response.Items)
            {
                sb.AppendLine($"- **Span {span.SpanId}** — Cluster: {span.ClusterLabel}");
                sb.AppendLine($"  - Similarity: {span.SimilarityScore:P1} (distance: {span.Distance:F4})");
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.get_error_timeline", Title = "Get Error Timeline",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Get error occurrence frequency over time for trend analysis.

                 Shows how often an error occurs across time buckets.
                 Useful for identifying spikes, regressions, or gradual improvements.

                 Example queries:
                 - Last 24 hours by hour: get_error_timeline(issueId="abc123")
                 - Last week by 6h buckets: get_error_timeline(issueId="abc123", hours=168, bucketMinutes=360)
                 - Last hour by minute: get_error_timeline(issueId="abc123", hours=1, bucketMinutes=1)

                 Returns: Time-bucketed occurrence counts with ASCII sparkline
                 """)]
    public Task<string> GetErrorTimelineAsync(
        [Description("The issue ID to get timeline for")] string issueId,
        [Description("Time window in hours (default: 24)")] int hours = 24,
        [Description("Bucket size in minutes (default: 60)")] int bucketMinutes = 60) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            TimelineResponse? response = await client.GetFromJsonAsync<TimelineResponse>(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/timeline?hours={hours}&bucketMinutes={bucketMinutes}",
                ErrorJsonContext.Default.TimelineResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count is 0)
                return $"No timeline data available for issue '{issueId}'.";

            StringBuilder sb = new();
            sb.AppendLine($"# Error Timeline — Issue {issueId}");
            sb.AppendLine($"Window: {hours}h, bucket: {bucketMinutes}min");
            sb.AppendLine();
            sb.AppendLine("| Time | Count |");
            sb.AppendLine("|------|-------|");

            int maxCount = 0;
            foreach (TimelineBucketDto bucket in response.Items)
            {
                sb.AppendLine($"| {bucket.Bucket:yyyy-MM-dd HH:mm} | {bucket.Count:N0} |");
                if (bucket.Count > maxCount)
                    maxCount = bucket.Count;
            }

            sb.AppendLine();
            sb.AppendLine("**Sparkline:**");
            sb.Append('`');
            string[] blocks = [" ", "\u2581", "\u2582", "\u2583", "\u2584", "\u2585", "\u2586", "\u2587", "\u2588"];
            foreach (TimelineBucketDto bucket in response.Items)
            {
                int index = maxCount > 0 ? (int)((double)bucket.Count / maxCount * (blocks.Length - 1)) : 0;
                sb.Append(blocks[index]);
            }

            sb.Append('`');
            sb.AppendLine();

            return sb.ToString();
        });
}

#region DTOs

internal sealed record ErrorIssueListResponse(
    [property: JsonPropertyName("items")] List<ErrorIssueDto>? Items,
    [property: JsonPropertyName("total")] int Total);

internal sealed record ErrorIssueDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("error_type")] string ErrorType,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("occurrence_count")] long OccurrenceCount,
    [property: JsonPropertyName("affected_users_count")] int AffectedUsersCount,
    [property: JsonPropertyName("first_seen_at")] DateTime FirstSeenAt,
    [property: JsonPropertyName("last_seen_at")] DateTime LastSeenAt,
    [property: JsonPropertyName("culprit")] string? Culprit,
    [property: JsonPropertyName("assigned_to")] string? AssignedTo,
    [property: JsonPropertyName("fingerprint")] string Fingerprint);

internal sealed record ErrorIssueEventDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("trace_id")] string? TraceId,
    [property: JsonPropertyName("span_id")] string? SpanId,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("stack_trace")] string? StackTrace,
    [property: JsonPropertyName("environment")] string? Environment,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp);

internal sealed record ErrorIssueEventsResponse(
    [property: JsonPropertyName("items")] List<ErrorIssueEventDto>? Items,
    [property: JsonPropertyName("total")] int Total);

internal sealed record TimelineBucketDto(
    [property: JsonPropertyName("bucket")] DateTime Bucket,
    [property: JsonPropertyName("count")] int Count);

internal sealed record TimelineResponse(
    [property: JsonPropertyName("items")] List<TimelineBucketDto>? Items);

internal sealed record SimilarSpanDto(
    [property: JsonPropertyName("span_id")] string SpanId,
    [property: JsonPropertyName("cluster_label")] string ClusterLabel,
    [property: JsonPropertyName("distance")] double Distance,
    [property: JsonPropertyName("similarity_score")] double SimilarityScore);

internal sealed record SimilarSpansResponse(
    [property: JsonPropertyName("items")] List<SimilarSpanDto>? Items);

#endregion

[JsonSerializable(typeof(ErrorIssueListResponse))]
[JsonSerializable(typeof(ErrorIssueDto))]
[JsonSerializable(typeof(ErrorIssueEventsResponse))]
[JsonSerializable(typeof(TimelineResponse))]
[JsonSerializable(typeof(SimilarSpansResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class ErrorJsonContext : JsonSerializerContext;
