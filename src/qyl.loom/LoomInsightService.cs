using Microsoft.Extensions.AI;

namespace Qyl.Loom;

/// <summary>
///     Generates pre-investigation insights for the Loom sidebar panel.
///     Produces the "What Happened" / "Initial Guess" / "In the Trace" summary
///     shown before the user starts interactive exploration.
/// </summary>
public sealed partial class LoomInsightService(
    DuckDbStore store,
    IssueService issueService,
    ILogger<LoomInsightService> logger,
    IChatClient? llm = null)
{
    public async Task<LoomInsight?> GenerateInsightAsync(string issueId, CancellationToken ct = default)
    {
        var issue = await store.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        if (issue is null)
        {
            LogIssueNotFound(issueId);
            return null;
        }

        var events = await issueService
            .GetEventsAsync(issueId, 5, ct).ConfigureAwait(false);

        LogGeneratingInsight(issueId, events.Count);

        if (llm is not null)
            return await GenerateWithLlmAsync(issueId, issue, events, ct).ConfigureAwait(false);

        return GenerateHeuristic(issueId, issue, events);
    }

    // ── LLM path ──────────────────────────────────────────────────────────────

    private async Task<LoomInsight> GenerateWithLlmAsync(
        string issueId, IssueSummary issue,
        IReadOnlyList<ErrorIssueEventRow> events, CancellationToken ct)
    {
        var context = BuildContextBlock(issue, events);
        var prompt = $"{LoomPrompts.InsightGeneration}\n\nError details:\n{context}";

        try
        {
            var response = await llm!.GetResponseAsync(prompt, cancellationToken: ct)
                .ConfigureAwait(false);

            var text = response.Text ?? "{}";
            var parsed = TryParseInsight(text, issueId);
            if (parsed is not null)
                return parsed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLlmInsightFailed(issueId, ex);
        }

        return GenerateHeuristic(issueId, issue, events);
    }

    // ── Heuristic fallback ────────────────────────────────────────────────────

    internal static LoomInsight GenerateHeuristic(
        string issueId, IssueSummary issue,
        IReadOnlyList<ErrorIssueEventRow> events)
    {
        var whatHappened = $"{issue.ErrorType} occurred " +
                           $"{issue.EventCount} time{(issue.EventCount == 1 ? "" : "s")} " +
                           $"since {issue.FirstSeen:yyyy-MM-dd HH:mm} UTC.";

        var initialGuess = issue.ErrorType switch
        {
            { } t when t.Contains("NullReference", StringComparison.OrdinalIgnoreCase) =>
                "A null reference is being accessed — likely a missing null check or uninitialized dependency.",
            { } t when t.Contains("NetworkError", StringComparison.OrdinalIgnoreCase) || t.Contains("HttpRequest", StringComparison.OrdinalIgnoreCase) =>
                "A network request is failing — this may be a symptom of a backend exception or connectivity issue.",
            { } t when t.Contains("Timeout", StringComparison.OrdinalIgnoreCase) =>
                "An operation is timing out — check for slow queries, external service latency, or deadlocks.",
            { } t when t.Contains("ArgumentException", StringComparison.OrdinalIgnoreCase) || t.Contains("InvalidOperation", StringComparison.OrdinalIgnoreCase) =>
                "An invalid argument or state is being passed — check input validation and preconditions.",
            { } t when t.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) || t.Contains("Forbidden", StringComparison.OrdinalIgnoreCase) =>
                "An authentication or authorization check is failing — verify tokens, permissions, and middleware.",
            _ => $"A {issue.ErrorType} is being thrown — investigate the stack trace for the originating call site."
        };

        var inTheTrace = events.Count > 0 && events[0].StackTrace is not null
            ? $"The most recent event shows: {events[0].Message ?? issue.ErrorType}"
            : null;

        return new LoomInsight
        {
            IssueId = issueId,
            WhatHappened = whatHappened,
            InitialGuess = initialGuess,
            InTheTrace = inTheTrace,
            Resources = BuildResources(issue)
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildContextBlock(IssueSummary issue, IReadOnlyList<ErrorIssueEventRow> events)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Type: {issue.ErrorType}");
        sb.AppendLine($"Message: {issue.ErrorMessage ?? "N/A"}");
        sb.AppendLine($"Occurrences: {issue.EventCount}");
        sb.AppendLine($"First seen: {issue.FirstSeen:O}");
        sb.AppendLine($"Last seen: {issue.LastSeen:O}");
        sb.AppendLine($"Status: {issue.Status}");

        if (events.Count > 0)
        {
            sb.AppendLine("\nRecent events:");
            foreach (var e in events)
            {
                sb.AppendLine($"- [{e.Timestamp:O}] {e.Message ?? "no message"}");
                if (e.StackTrace is not null)
                    sb.AppendLine($"  Stack: {e.StackTrace[..Math.Min(500, e.StackTrace.Length)]}");
                if (e.Environment is not null)
                    sb.AppendLine($"  Env: {e.Environment}");
            }
        }

        return sb.ToString();
    }

    private static LoomResource[] BuildResources(IssueSummary issue)
    {
        List<LoomResource> resources = [];

        if (issue.ErrorType.Contains("NetworkError", StringComparison.OrdinalIgnoreCase) ||
            issue.ErrorType.Contains("Fetch", StringComparison.OrdinalIgnoreCase))
        {
            resources.Add(new LoomResource(
                "Failed to Fetch errors",
                null,
                "Failed to Fetch errors occur when there is an error with the Fetch API. " +
                "Check backend response status codes and CORS configuration."));
        }

        if (issue.ErrorType.Contains("NullReference", StringComparison.OrdinalIgnoreCase))
        {
            resources.Add(new LoomResource(
                "NullReferenceException patterns",
                null,
                "Common causes: uninitialized fields, missing DI registrations, " +
                "async operations returning null, database queries with no results."));
        }

        return [.. resources];
    }

    private static LoomInsight? TryParseInsight(string text, string issueId)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        var json = text.AsSpan(start, end - start + 1);
        try
        {
            var r = JsonSerializer.Deserialize(json, LoomInsightJsonContext.Default.InsightLlmResponse);
            if (r is null) return null;

            return new LoomInsight
            {
                IssueId = issueId,
                WhatHappened = r.WhatHappened ?? "Unknown",
                InitialGuess = r.InitialGuess ?? "Unable to determine",
                InTheTrace = r.InTheTrace,
                Resources = r.Resources?.Select(static x =>
                    new LoomResource(x.Title ?? "Resource", x.Url, x.Description ?? "")).ToArray() ?? []
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ── Log methods ───────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Generating Loom insight for issue {IssueId}, {EventCount} events")]
    private partial void LogGeneratingInsight(string issueId, int eventCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Issue {IssueId} not found for Loom insight")]
    private partial void LogIssueNotFound(string issueId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "LLM insight generation failed for issue {IssueId}, falling back to heuristic")]
    private partial void LogLlmInsightFailed(string issueId, Exception ex);
}

// ── Internal LLM response model ──────────────────────────────────────────────

internal sealed record InsightLlmResponse
{
    [JsonPropertyName("what_happened")] public string? WhatHappened { get; init; }

    [JsonPropertyName("initial_guess")] public string? InitialGuess { get; init; }

    [JsonPropertyName("in_the_trace")] public string? InTheTrace { get; init; }

    [JsonPropertyName("resources")] public InsightLlmResource[]? Resources { get; init; }
}

internal sealed record InsightLlmResource
{
    [JsonPropertyName("title")] public string? Title { get; init; }

    [JsonPropertyName("url")] public string? Url { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }
}

[JsonSerializable(typeof(InsightLlmResponse))]
internal partial class LoomInsightJsonContext : JsonSerializerContext;
