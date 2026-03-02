using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Sentry;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for Sentry events and distributed traces.
///     Covers: list_issue_events, get_trace, search_errors.
/// </summary>
[McpServerToolType]
public sealed class qylEventTools(HttpClient client)
{
    // ── Events for an issue ───────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_list_issue_events")]
    [Description("""
                 List recent events for a Sentry issue.

                 Each event is one occurrence of the issue — useful for seeing
                 how error details vary across occurrences (different users,
                 environments, stack frames).

                 Use sentry_get_issue to get the issue ID first.

                 Returns: Event list with timestamps, environments, and user info
                 """)]
    public Task<string> ListIssueEventsAsync(
        [Description("Issue ID or short ID")] string issueId,
        [Description("Maximum events to return (default: 10)")]
        int limit = 10) =>
        qylHelper.ExecuteAsync(async () =>
        {
            var events = await client.GetFromJsonAsync<SentryEventDto[]>(
                $"issues/{Uri.EscapeDataString(issueId)}/events/?limit={limit}&full=true",
                SentryEventJsonContext.Default.SentryEventDtoArray).ConfigureAwait(false);

            if (events is null || events.Length is 0)
                return $"No events found for issue '{issueId}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Events for Issue {issueId} ({events.Length})");
            sb.AppendLine();

            foreach (var evt in events)
            {
                sb.AppendLine($"## {evt.DateCreated:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"- **Event ID:** `{evt.EventId}`");
                if (!string.IsNullOrEmpty(evt.Environment))
                    sb.AppendLine($"- **Environment:** {evt.Environment}");
                if (!string.IsNullOrEmpty(evt.Release))
                    sb.AppendLine($"- **Release:** {evt.Release}");
                if (evt.User is not null)
                    sb.AppendLine($"- **User:** {evt.User.Username ?? evt.User.Email ?? evt.User.IpAddress ?? "anonymous"}");

                // First stack frame from the exception
                var frame = evt.Entries
                    ?.FirstOrDefault(e => e.Type == "exception")
                    ?.Data?.Values?.FirstOrDefault()
                    ?.Stacktrace?.Frames?.LastOrDefault(f => f.InApp is true);

                if (frame is not null)
                {
                    sb.AppendLine($"- **Top frame:** `{frame.Function}` in {frame.Filename}:{frame.LineNo}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        });

    // ── Trace details ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_get_trace")]
    [Description("""
                 Get the distributed trace for a Sentry trace ID.

                 Shows all spans across services for a single request.
                 The trace ID can be found in Sentry issue events or URLs.

                 Returns: Span hierarchy with timing, services, and errors
                 """)]
    public Task<string> GetTraceAsync(
        [Description("Organization slug")] string orgSlug,
        [Description("Trace ID (hex string)")] string traceId) =>
        qylHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<SentryTraceResponseDto>(
                $"organizations/{Uri.EscapeDataString(orgSlug)}/events-trace/{Uri.EscapeDataString(traceId)}/",
                SentryEventJsonContext.Default.SentryTraceResponseDto).ConfigureAwait(false);

            if (response is null) return $"Trace '{traceId}' not found in org '{orgSlug}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Trace: {traceId}");
            sb.AppendLine();

            // Flatten top-level transactions + errors
            var transactions = response.Transactions ?? [];
            var errors = response.Errors ?? [];

            sb.AppendLine($"**Transactions:** {transactions.Count}  **Errors:** {errors.Count}");
            sb.AppendLine();

            foreach (var txn in transactions.OrderBy(t => t.StartTimestamp))
            {
                var durationMs = (txn.Timestamp - txn.StartTimestamp) * 1000;
                var statusIcon = txn.Status == "ok" ? "✅" : "🔴";
                sb.AppendLine($"{statusIcon} **{txn.Transaction}** ({durationMs:F0}ms)");
                sb.AppendLine($"  Service: {txn.Project} | Span: `{txn.SpanId}`");
            }

            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Errors in trace");
                foreach (var err in errors)
                    sb.AppendLine($"- 🔴 [{err.IssueId}] {err.Title} — `{err.SpanId}`");
            }

            return sb.ToString();
        });

    // ── Search errors ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_search_errors")]
    [Description("""
                 Search Sentry error events using EAP (Events Analytics Platform).

                 Searches across all events — not just issue summaries.
                 Useful for finding specific error messages, stack frames, or user IDs.

                 Parameters:
                 - query: Sentry event query syntax
                 - environments: comma-separated list (e.g., "production,staging")

                 Returns: Matching error events with details
                 """)]
    public Task<string> SearchErrorsAsync(
        [Description("Organization slug")] string orgSlug,
        [Description("Project slug")] string projectSlug,
        [Description("Search query (e.g., 'stack.function:PaymentService*')")]
        string query,
        [Description("Environment filter (e.g., 'production')")]
        string? environment = null,
        [Description("Maximum results (default: 20)")]
        int limit = 20) =>
        qylHelper.ExecuteAsync(async () =>
        {
            var url = $"projects/{Uri.EscapeDataString(orgSlug)}/{Uri.EscapeDataString(projectSlug)}/events/"
                      + $"?query={Uri.EscapeDataString(query)}&limit={limit}&full=false";
            if (!string.IsNullOrEmpty(environment))
                url += $"&environment={Uri.EscapeDataString(environment)}";

            var response = await client.GetFromJsonAsync<SentryEventsPageDto>(
                url, SentryEventJsonContext.Default.SentryEventsPageDto).ConfigureAwait(false);

            var events = response?.Data;
            if (events is null || events.Count is 0)
                return $"No error events found for query '{query}' in {orgSlug}/{projectSlug}.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Error Events ({events.Count} results)");
            sb.AppendLine($"Query: `{query}`");
            sb.AppendLine();

            foreach (var evt in events)
            {
                sb.AppendLine($"## {evt.DateCreated:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"- **Title:** {evt.Title}");
                if (!string.IsNullOrEmpty(evt.EventId))
                    sb.AppendLine($"- **Event ID:** `{evt.EventId}`");
                if (!string.IsNullOrEmpty(evt.GroupId))
                    sb.AppendLine($"- **Issue:** `{evt.GroupId}`");
                sb.AppendLine();
            }

            return sb.ToString();
        });
}

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

internal sealed record SentryEventDto(
    [property: JsonPropertyName("eventID")] string EventId,
    [property: JsonPropertyName("dateCreated")] DateTimeOffset DateCreated,
    [property: JsonPropertyName("environment")] string? Environment,
    [property: JsonPropertyName("release")] string? Release,
    [property: JsonPropertyName("user")] SentryEventUserDto? User,
    [property: JsonPropertyName("entries")] List<SentryEntryDto>? Entries);

internal sealed record SentryEventUserDto(
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("ipAddress")] string? IpAddress);

internal sealed record SentryEntryDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] SentryEntryDataDto? Data);

internal sealed record SentryEntryDataDto(
    [property: JsonPropertyName("values")] List<SentryExceptionDto>? Values);

internal sealed record SentryExceptionDto(
    [property: JsonPropertyName("stacktrace")] SentryStacktraceDto? Stacktrace);

internal sealed record SentryStacktraceDto(
    [property: JsonPropertyName("frames")] List<SentryFrameDto>? Frames);

internal sealed record SentryFrameDto(
    [property: JsonPropertyName("function")] string? Function,
    [property: JsonPropertyName("filename")] string? Filename,
    [property: JsonPropertyName("lineNo")] int? LineNo,
    [property: JsonPropertyName("inApp")] bool? InApp);

internal sealed record SentryTraceResponseDto(
    [property: JsonPropertyName("transactions")] List<SentryTraceTransactionDto>? Transactions,
    [property: JsonPropertyName("errors")] List<SentryTraceErrorDto>? Errors);

internal sealed record SentryTraceTransactionDto(
    [property: JsonPropertyName("transaction")] string Transaction,
    [property: JsonPropertyName("project")] string? Project,
    [property: JsonPropertyName("spanId")] string? SpanId,
    [property: JsonPropertyName("startTimestamp")] double StartTimestamp,
    [property: JsonPropertyName("timestamp")] double Timestamp,
    [property: JsonPropertyName("status")] string? Status);

internal sealed record SentryTraceErrorDto(
    [property: JsonPropertyName("issueId")] string? IssueId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("spanId")] string? SpanId);

internal sealed record SentryEventsPageDto(
    [property: JsonPropertyName("data")] List<SentryEventSummaryDto>? Data);

internal sealed record SentryEventSummaryDto(
    [property: JsonPropertyName("eventID")] string? EventId,
    [property: JsonPropertyName("groupID")] string? GroupId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("dateCreated")] DateTimeOffset DateCreated);

// ─────────────────────────────────────────────────────────────────────────────
// JSON context (AOT)
// ─────────────────────────────────────────────────────────────────────────────

[JsonSerializable(typeof(SentryEventDto[]))]
[JsonSerializable(typeof(SentryTraceResponseDto))]
[JsonSerializable(typeof(SentryEventsPageDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class SentryEventJsonContext : JsonSerializerContext;
