using System.Net;
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
        var trace = await GetAsync(
            $"/api/v1/traces/{Uri.EscapeDataString(traceId)}",
            SummaryJsonContext.Default.TraceResponseDto,
            ct).ConfigureAwait(false);

        var spans = trace?.Spans;
        if (spans is null || spans.Count is 0)
            return $"Trace '{traceId}' not found or contains no spans.";

        var rawContext = RenderTraceContext(traceId, trace, spans);
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
        GetCoreAsync(path, jsonTypeInfo, ct);

    private async Task<T?> GetCoreAsync<T>(
        string path,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken ct)
    {
        using var response = await client.GetAsync(path, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(jsonTypeInfo, ct).ConfigureAwait(false);
    }

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

    private static string RenderTraceContext(string traceId, TraceResponseDto trace, List<TraceSpanDto> spans)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Trace ID: {traceId}");
        sb.AppendLine($"Total Spans: {spans.Count}");

        if (trace.DurationMs.HasValue)
            sb.AppendLine($"Total Duration: {trace.DurationMs.Value:F1}ms");
        if (trace.Status is not null)
            sb.AppendLine($"Status: {trace.Status}");

        var errorCount = spans.Count(static s => IsErrorStatus(s.Status));
        if (errorCount > 0)
            sb.AppendLine($"Error Spans: {errorCount}");

        sb.AppendLine("\nSpans:");
        foreach (var span in spans.OrderBy(static s => s.StartTime, StringComparer.Ordinal))
        {
            var statusLabel = IsErrorStatus(span.Status) ? " [ERROR]" : "";
            var parentInfo = span.ParentSpanId is not null ? $" (parent: {span.ParentSpanId})" : " (root)";

            sb.AppendLine($"- {span.Name}{statusLabel} - {span.DurationMs:F1}ms");
            sb.AppendLine($"  Service: {span.ServiceName ?? "unknown"}, SpanID: {span.SpanId}{parentInfo}");

            if (IsErrorStatus(span.Status) && span.StatusMessage is not null)
                sb.AppendLine($"  Error: {span.StatusMessage}");
        }

        return sb.ToString();
    }

    private static string RenderSessionContext(SessionDto session, SessionSpansResponse? spansResponse)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Session ID: {session.SessionId}");
        if (session.Services is { Count: > 0 })
            sb.AppendLine($"Services: {string.Join(", ", session.Services)}");
        if (session.StartTime is not null)
            sb.AppendLine($"Start: {session.StartTime}");
        if (session.LastActivity is not null)
            sb.AppendLine($"Last Activity: {session.LastActivity}");
        sb.AppendLine($"Duration: {session.DurationMs:F1}ms");
        sb.AppendLine($"Span Count: {session.SpanCount}");
        sb.AppendLine($"Error Count: {session.ErrorCount}");

        if (spansResponse?.Spans is { Count: > 0 })
        {
            sb.AppendLine($"\nSpans ({spansResponse.Spans.Count} shown):");
            foreach (var span in spansResponse.Spans.OrderBy(static s => s.StartTime, StringComparer.Ordinal))
            {
                var statusLabel = IsErrorStatus(span.Status) ? " [ERROR]" : "";

                sb.AppendLine($"- {span.Name}{statusLabel} - {span.DurationMs:F1}ms");
                if (span.ServiceName is not null)
                    sb.AppendLine($"  Service: {span.ServiceName}");
                if (IsErrorStatus(span.Status) && span.StatusMessage is not null)
                    sb.AppendLine($"  Error: {span.StatusMessage}");
            }
        }

        return sb.ToString();
    }

    private static bool IsErrorStatus(string? status) =>
        string.Equals(status, "error", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}

internal sealed record SummaryIssueDto(
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("error_type")]
    string ErrorType,
    [property: JsonPropertyName("category")]
    string Category,
    [property: JsonPropertyName("status")]
    string Status,
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
    List<SummaryEventDto>? Items);

internal sealed record TraceSpanDto(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string Name,
    string? ServiceName,
    string? StartTime,
    double DurationMs,
    string Status,
    string? StatusMessage);

internal sealed record TraceResponseDto(
    string? TraceId,
    List<TraceSpanDto>? Spans,
    TraceSpanDto? RootSpan,
    double? DurationMs,
    string? Status);

internal sealed record SessionDto(
    string SessionId,
    IReadOnlyList<string>? Services,
    string? StartTime,
    string? LastActivity,
    double DurationMs,
    long SpanCount,
    long ErrorCount);

internal sealed record SessionSpanDto(
    string SpanId,
    string Name,
    string? ServiceName,
    string? StartTime,
    double DurationMs,
    string Status,
    string? StatusMessage);

internal sealed record SessionSpansResponse(
    List<SessionSpanDto>? Spans);

[JsonSerializable(typeof(SummaryIssueDto))]
[JsonSerializable(typeof(SummaryEventsResponse))]
[JsonSerializable(typeof(TraceResponseDto))]
[JsonSerializable(typeof(SessionDto))]
[JsonSerializable(typeof(SessionSpansResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class SummaryJsonContext : JsonSerializerContext;
