using Qyl.Loom.Agents;

namespace Qyl.Loom.Exploration;

/// <summary>
///     Generates pre-investigation insights for the sidebar panel.
///     Produces "What Happened" / "Initial Guess" / "In the Trace" summary.
///     Uses <see cref="CollectorClient" /> instead of direct DuckDB access.
/// </summary>
public sealed partial class ExplorationInsightService(
    CollectorClient collector,
    ILogger<ExplorationInsightService> logger,
    IQylLoomAgentsBuilder agents)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, CachedInsight> _cache = new(StringComparer.Ordinal);

    public async Task<ExplorationInsight?> GenerateInsightAsync(string issueId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(issueId, out var cached) && cached.ExpiresAt > TimeProvider.System.GetUtcNow())
        {
            LogCacheHit(issueId);
            return cached.Insight;
        }

        var issue = await collector.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        if (issue is null)
        {
            LogIssueNotFound(issueId);
            return null;
        }

        var events = await collector.GetIssueEventsAsync(issueId, 5, ct).ConfigureAwait(false);

        LogGeneratingInsight(issueId, events.Count);

        var insight = agents.IsConfigured
            ? await GenerateWithLlmAsync(issueId, issue, events, ct).ConfigureAwait(false)
            : GenerateHeuristic(issueId, issue, events);

        _cache[issueId] = new CachedInsight(insight, TimeProvider.System.GetUtcNow() + CacheTtl);
        return insight;
    }

    private async Task<ExplorationInsight> GenerateWithLlmAsync(
        string issueId, IssueSummary issue,
        IReadOnlyList<IssueEventDto> events, CancellationToken ct)
    {
        var context = BuildContextBlock(issue, events);

        try
        {
            var agent = agents.BuildExplorationInsightAgent();

            var response = await agent.RunAsync(
                $"Error details:\n{context}",
                cancellationToken: ct).ConfigureAwait(false);

            var parsed = TryParseInsight(response.Text, issueId);
            if (parsed is not null) return parsed;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LogLlmInsightFailed(issueId, ex);
        }

        return GenerateHeuristic(issueId, issue, events);
    }

    internal static ExplorationInsight GenerateHeuristic(
        string issueId, IssueSummary issue,
        IReadOnlyList<IssueEventDto> events)
    {
        var whatHappened = $"{issue.ErrorType} occurred " +
                           $"{issue.EventCount} time{(issue.EventCount == 1 ? "" : "s")} " +
                           $"since {issue.FirstSeen:yyyy-MM-dd HH:mm} UTC.";

        var initialGuess = issue.ErrorType switch
        {
            { } t when t.ContainsIgnoreCase("NullReference") =>
                "A null reference is being accessed — likely a missing null check or uninitialized dependency.",
            { } t when t.ContainsIgnoreCase("NetworkError")
                       || t.ContainsIgnoreCase("HttpRequest") =>
                "A network request is failing — this may be a symptom of a backend exception or connectivity issue.",
            { } t when t.ContainsIgnoreCase("Timeout") =>
                "An operation is timing out — check for slow queries, external service latency, or deadlocks.",
            _ => $"A {issue.ErrorType} is being thrown — investigate the stack trace for the originating call site."
        };

        var inTheTrace = events.Count > 0 && events[0].StackTrace is not null
            ? $"The most recent event shows: {events[0].Message ?? issue.ErrorType}"
            : null;

        return new ExplorationInsight
        {
            IssueId = issueId, WhatHappened = whatHappened, InitialGuess = initialGuess, InTheTrace = inTheTrace
        };
    }

    private static string BuildContextBlock(IssueSummary issue, IReadOnlyList<IssueEventDto> events)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Type: {issue.ErrorType}");
        sb.AppendLine($"Message: {issue.ErrorMessage ?? "N/A"}");
        sb.AppendLine($"Occurrences: {issue.EventCount}");
        sb.AppendLine($"First seen: {issue.FirstSeen:O}");
        sb.AppendLine($"Last seen: {issue.LastSeen:O}");

        foreach (var e in events)
        {
            sb.AppendLine($"- [{e.Timestamp:O}] {e.Message ?? "no message"}");
            if (e.StackTrace is not null)
                sb.AppendLine($"  Stack: {e.StackTrace[..Math.Min(500, e.StackTrace.Length)]}");
        }

        return sb.ToString();
    }

    private static ExplorationInsight? TryParseInsight(string text, string issueId)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        try
        {
            var r = JsonSerializer.Deserialize(text.AsSpan(start, end - start + 1),
                ExplorationJsonContext.Default.InsightLlmResponse);
            if (r is null) return null;

            return new ExplorationInsight
            {
                IssueId = issueId,
                WhatHappened = r.WhatHappened ?? "Unknown",
                InitialGuess = r.InitialGuess ?? "Unable to determine",
                InTheTrace = r.InTheTrace
            };
        }
        catch (JsonException) { return null; }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Generating insight for issue {IssueId}, {EventCount} events")]
    private partial void LogGeneratingInsight(string issueId, int eventCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Issue {IssueId} not found for insight")]
    private partial void LogIssueNotFound(string issueId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM insight failed for {IssueId}, using heuristic")]
    private partial void LogLlmInsightFailed(string issueId, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache hit for insight {IssueId}")]
    private partial void LogCacheHit(string issueId);

    private sealed record CachedInsight(ExplorationInsight Insight, DateTimeOffset ExpiresAt);
}
