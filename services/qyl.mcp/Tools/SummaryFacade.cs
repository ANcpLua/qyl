using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Agents.AI;
using qyl.mcp.Agents;

namespace qyl.mcp.Tools;

internal sealed class SummaryFacade(HttpClient client, IQylMcpAgentsBuilder agents)
{
    public async Task<string> SummarizeErrorAsync(string issueId, CancellationToken ct)
    {
        var escapedIssueId = Uri.EscapeDataString(issueId);
        var issueTask = GetAsync(
            $"/api/v1/issues/{escapedIssueId}",
            SummaryJsonContext.Default.SummaryIssueDto,
            ct);

        var eventsTask = GetAsync(
            $"/api/v1/issues/{escapedIssueId}/events?limit=5",
            SummaryJsonContext.Default.SummaryEventsResponse,
            ct);

        await Task.WhenAll(issueTask, eventsTask).ConfigureAwait(false);

        var issue = await issueTask.ConfigureAwait(false);
        if (issue is null)
            return $"Error issue '{issueId}' not found.";

        var rawContext = RenderErrorContext(issue, await eventsTask.ConfigureAwait(false));
        return await CompleteWithAgentAsync(
            "Error Summary",
            rawContext,
            agents.BuildSummarizeErrorAgent,
            ct).ConfigureAwait(false);
    }

    public async Task<string> SummarizeTraceAsync(string traceId, CancellationToken ct)
    {
        var spans = await GetAsync(
            $"/api/v1/traces/{Uri.EscapeDataString(traceId)}",
            SummaryJsonContext.Default.ListTraceSpanDto,
            ct).ConfigureAwait(false);

        if (spans is null || spans.Count is 0)
            return $"Trace '{traceId}' not found or contains no spans.";

        var rawContext = RenderTraceContext(traceId, spans);
        return await CompleteWithAgentAsync(
            "Trace Summary",
            rawContext,
            agents.BuildSummarizeTraceAgent,
            ct).ConfigureAwait(false);
    }

    public async Task<string> SummarizeSessionAsync(string sessionId, CancellationToken ct)
    {
        var escapedSessionId = Uri.EscapeDataString(sessionId);
        var sessionTask = GetAsync(
            $"/api/v1/sessions/{escapedSessionId}",
            SummaryJsonContext.Default.SessionDto,
            ct);

        var spansTask = GetAsync(
            $"/api/v1/sessions/{escapedSessionId}/spans?limit=50",
            SummaryJsonContext.Default.SessionSpansResponse,
            ct);

        await Task.WhenAll(sessionTask, spansTask).ConfigureAwait(false);

        var session = await sessionTask.ConfigureAwait(false);
        if (session is null)
            return $"Session '{sessionId}' not found.";

        var rawContext = RenderSessionContext(session, await spansTask.ConfigureAwait(false));
        return await CompleteWithAgentAsync(
            "Session Summary",
            rawContext,
            agents.BuildSummarizeSessionAgent,
            ct).ConfigureAwait(false);
    }

    private Task<T?> GetAsync<T>(
        string path,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken ct) =>
        client.GetFromJsonAsync(path, jsonTypeInfo, ct);

    private async Task<string> CompleteWithAgentAsync(
        string title,
        string rawContext,
        Func<AIAgent> buildAgent,
        CancellationToken ct)
    {
        var safeRawContext = SummaryCredentialRedactor.Redact(rawContext);
        if (!agents.IsConfigured)
            return $"# {title} (raw data -- no LLM configured)\n\n{safeRawContext}";

        var response = await buildAgent().RunAsync(safeRawContext, cancellationToken: ct).ConfigureAwait(false);
        return response.Text is { Length: > 0 } text ? text : "Summary generation produced no output.";
    }

    private static string RenderErrorContext(SummaryIssueDto issue, SummaryEventsResponse? eventsResponse)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Error Issue: {issue.Title}");
        sb.AppendLine($"Type: {issue.ErrorType}, Category: {issue.Category}");
        sb.AppendLine($"Status: {issue.Status}, Priority: {issue.Priority}");
        sb.AppendLine($"Occurrences: {issue.OccurrenceCount}, Affected Users: {issue.AffectedUsersCount}");
        sb.AppendLine($"First Seen: {issue.FirstSeenAt:u}, Last Seen: {issue.LastSeenAt:u}");
        if (issue.Culprit is not null)
            sb.AppendLine($"Culprit: {issue.Culprit}");

        if (eventsResponse?.Items is { Count: > 0 })
        {
            sb.AppendLine("\nRecent Events:");
            foreach (var evt in eventsResponse.Items)
            {
                sb.AppendLine($"- [{evt.Timestamp:u}] {evt.Message}");
                if (evt.StackTrace is not null)
                    sb.AppendLine($"  Stack: {Truncate(evt.StackTrace, 500)}");
            }
        }

        return sb.ToString();
    }

    private static string RenderTraceContext(string traceId, List<TraceSpanDto> spans)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Trace ID: {traceId}");
        sb.AppendLine($"Total Spans: {spans.Count}");

        var minStart = spans.Min(static s => s.StartTimeUnixNano);
        var maxEnd = spans.Max(static s => s.StartTimeUnixNano + s.DurationNs);
        var totalDurationMs = (maxEnd - minStart) / 1_000_000.0;
        sb.AppendLine($"Total Duration: {totalDurationMs:F1}ms");

        var errorCount = spans.Count(static s => s.StatusCode == 2);
        if (errorCount > 0)
            sb.AppendLine($"Error Spans: {errorCount}");

        sb.AppendLine("\nSpans:");
        foreach (var span in spans.OrderBy(static s => s.StartTimeUnixNano))
        {
            var durationMs = span.DurationNs / 1_000_000.0;
            var statusLabel = span.StatusCode == 2 ? " [ERROR]" : "";
            var parentInfo = span.ParentSpanId is not null ? $" (parent: {span.ParentSpanId})" : " (root)";

            sb.AppendLine($"- {span.Name}{statusLabel} - {durationMs:F1}ms");
            sb.AppendLine($"  Service: {span.ServiceName ?? "unknown"}, SpanID: {span.SpanId}{parentInfo}");

            if (span is { StatusCode: 2, StatusMessage: not null })
                sb.AppendLine($"  Error: {span.StatusMessage}");
        }

        return sb.ToString();
    }

    private static string RenderSessionContext(SessionDto session, SessionSpansResponse? spansResponse)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Session ID: {session.SessionId}");
        if (session.ServiceName is not null)
            sb.AppendLine($"Service: {session.ServiceName}");
        if (session.StartTime.HasValue)
            sb.AppendLine($"Start: {session.StartTime.Value:u}");
        if (session.EndTime.HasValue)
            sb.AppendLine($"End: {session.EndTime.Value:u}");
        sb.AppendLine($"Span Count: {session.SpanCount}");
        sb.AppendLine($"Error Count: {session.ErrorCount}");

        if (spansResponse?.Items is { Count: > 0 })
        {
            sb.AppendLine($"\nSpans ({spansResponse.Items.Count} shown):");
            foreach (var span in spansResponse.Items)
            {
                var durationMs = span.DurationNs / 1_000_000.0;
                var statusLabel = span.StatusCode == 2 ? " [ERROR]" : "";

                sb.AppendLine($"- {span.Name}{statusLabel} - {durationMs:F1}ms");
                if (span.ServiceName is not null)
                    sb.AppendLine($"  Service: {span.ServiceName}");
                if (span is { StatusCode: 2, StatusMessage: not null })
                    sb.AppendLine($"  Error: {span.StatusMessage}");
            }
        }

        return sb.ToString();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}

internal sealed record SummaryIssueDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("error_type")]
    string ErrorType,
    [property: JsonPropertyName("category")]
    string Category,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")]
    string Priority,
    [property: JsonPropertyName("occurrence_count")]
    long OccurrenceCount,
    [property: JsonPropertyName("affected_users_count")]
    int AffectedUsersCount,
    [property: JsonPropertyName("first_seen_at")]
    DateTime FirstSeenAt,
    [property: JsonPropertyName("last_seen_at")]
    DateTime LastSeenAt,
    [property: JsonPropertyName("culprit")]
    string? Culprit);

internal sealed record SummaryEventDto(
    [property: JsonPropertyName("timestamp")]
    DateTime Timestamp,
    [property: JsonPropertyName("message")]
    string? Message,
    [property: JsonPropertyName("stack_trace")]
    string? StackTrace);

internal sealed record SummaryEventsResponse(
    [property: JsonPropertyName("items")] List<SummaryEventDto>? Items);

internal sealed record TraceSpanDto(
    [property: JsonPropertyName("trace_id")]
    string TraceId,
    [property: JsonPropertyName("span_id")]
    string SpanId,
    [property: JsonPropertyName("parent_span_id")]
    string? ParentSpanId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("service_name")]
    string? ServiceName,
    [property: JsonPropertyName("start_time_unix_nano")]
    long StartTimeUnixNano,
    [property: JsonPropertyName("duration_ns")]
    long DurationNs,
    [property: JsonPropertyName("status_code")]
    int StatusCode,
    [property: JsonPropertyName("status_message")]
    string? StatusMessage);

internal sealed record SessionDto(
    [property: JsonPropertyName("session_id")]
    string SessionId,
    [property: JsonPropertyName("service_name")]
    string? ServiceName,
    [property: JsonPropertyName("start_time")]
    DateTime? StartTime,
    [property: JsonPropertyName("end_time")]
    DateTime? EndTime,
    [property: JsonPropertyName("span_count")]
    int SpanCount,
    [property: JsonPropertyName("error_count")]
    int ErrorCount);

internal sealed record SessionSpanDto(
    [property: JsonPropertyName("span_id")]
    string SpanId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("service_name")]
    string? ServiceName,
    [property: JsonPropertyName("start_time_unix_nano")]
    long StartTimeUnixNano,
    [property: JsonPropertyName("duration_ns")]
    long DurationNs,
    [property: JsonPropertyName("status_code")]
    int StatusCode,
    [property: JsonPropertyName("status_message")]
    string? StatusMessage);

internal sealed record SessionSpansResponse(
    [property: JsonPropertyName("items")] List<SessionSpanDto>? Items);

[JsonSerializable(typeof(SummaryIssueDto))]
[JsonSerializable(typeof(SummaryEventsResponse))]
[JsonSerializable(typeof(List<TraceSpanDto>))]
[JsonSerializable(typeof(SessionDto))]
[JsonSerializable(typeof(SessionSpansResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class SummaryJsonContext : JsonSerializerContext;
