using Microsoft.Extensions.AI;

namespace Qyl.Collector.Autofix;

/// <summary>
///     Generates pre-investigation insights for the Loom sidebar panel.
///     Produces "What Happened" / "Initial Guess" / "In the Trace" summary.
/// </summary>
public sealed partial class LoomInsightService(
    IssueContextBuilder contextBuilder,
    ILogger<LoomInsightService> logger,
    IChatClient? llm = null)
{
    public async Task<LoomInsight?> GenerateInsightAsync(string issueId, CancellationToken ct = default)
    {
        IssueContext context = await contextBuilder
            .BuildAsync(issueId, ct: ct)
            .ConfigureAwait(false);
        if (context.Issue is null)
        {
            LogIssueNotFound(issueId);
            return null;
        }

        LogGeneratingInsight(issueId, context.Events.Count);

        if (llm is not null)
            return await GenerateWithLlmAsync(issueId, context, ct).ConfigureAwait(false);

        return GenerateHeuristic(issueId, context.Issue, context.Events);
    }

    private async Task<LoomInsight> GenerateWithLlmAsync(
        string issueId,
        IssueContext context,
        CancellationToken ct)
    {
        try
        {
            ChatResponse response = await llm!.GetResponseAsync(
                $"{LoomPrompts.InsightGeneration}\n\nError details:\n{context.FormattedBlock}",
                cancellationToken: ct).ConfigureAwait(false);

            LoomInsight? parsed = TryParseInsight(response.Text ?? "{}", issueId);
            if (parsed is not null) return parsed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLlmInsightFailed(issueId, ex);
        }

        return GenerateHeuristic(issueId, context.Issue!, context.Events);
    }

    internal static LoomInsight GenerateHeuristic(
        string issueId, IssueSummary issue,
        IReadOnlyList<ErrorIssueEventRow> events)
    {
        string whatHappened = $"{issue.ErrorType} occurred " +
            $"{issue.EventCount} time{(issue.EventCount == 1 ? "" : "s")} " +
            $"since {issue.FirstSeen:yyyy-MM-dd HH:mm} UTC.";

        string initialGuess = issue.ErrorType switch
        {
            string t when t.ContainsIgnoreCase("NullReference") =>
                "A null reference is being accessed — likely a missing null check or uninitialized dependency.",
            string t when t.ContainsIgnoreCase("NetworkError") || t.ContainsIgnoreCase("HttpRequest") =>
                "A network request is failing — this may be a symptom of a backend exception or connectivity issue.",
            string t when t.ContainsIgnoreCase("Timeout") =>
                "An operation is timing out — check for slow queries, external service latency, or deadlocks.",
            _ => $"A {issue.ErrorType} is being thrown — investigate the stack trace for the originating call site."
        };

        string? inTheTrace = events.Count > 0 && events[0].StackTrace is not null
            ? $"The most recent event shows: {events[0].Message ?? issue.ErrorType}"
            : null;

        return new LoomInsight
        {
            IssueId = issueId,
            WhatHappened = whatHappened,
            InitialGuess = initialGuess,
            InTheTrace = inTheTrace
        };
    }

    private static LoomInsight? TryParseInsight(string text, string issueId)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        try
        {
            var r = JsonSerializer.Deserialize(text.AsSpan(start, end - start + 1),
                LoomInsightJsonContext.Default.InsightLlmResponse);
            if (r is null) return null;

            return new LoomInsight
            {
                IssueId = issueId,
                WhatHappened = r.WhatHappened ?? "Unknown",
                InitialGuess = r.InitialGuess ?? "Unable to determine",
                InTheTrace = r.InTheTrace
            };
        }
        catch (JsonException) { return null; }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generating Loom insight for issue {IssueId}, {EventCount} events")]
    private partial void LogGeneratingInsight(string issueId, int eventCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Issue {IssueId} not found for Loom insight")]
    private partial void LogIssueNotFound(string issueId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM insight failed for {IssueId}, using heuristic")]
    private partial void LogLlmInsightFailed(string issueId, Exception ex);
}

// ── Models ────────────────────────────────────────────────────────────────────

public sealed record LoomInsight
{
    public required string IssueId { get; init; }
    public required string WhatHappened { get; init; }
    public required string InitialGuess { get; init; }
    public string? InTheTrace { get; init; }
}

public sealed record LoomRootCause
{
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("steps")]
    public required LoomCausalStep[] Steps { get; init; }
}

public sealed record LoomCausalStep(
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("is_root_cause")] bool IsRootCause);

public sealed record LoomSolution
{
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("steps")]
    public required LoomSolutionStep[] Steps { get; init; }
}

public sealed record LoomSolutionStep(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description);

public sealed record LoomExploreRequest(
    [property: JsonPropertyName("user_context")] string? UserContext);

internal sealed record InsightLlmResponse
{
    [JsonPropertyName("what_happened")] public string? WhatHappened { get; init; }
    [JsonPropertyName("initial_guess")] public string? InitialGuess { get; init; }
    [JsonPropertyName("in_the_trace")] public string? InTheTrace { get; init; }
}

[JsonSerializable(typeof(InsightLlmResponse))]
[JsonSerializable(typeof(LoomInsight))]
[JsonSerializable(typeof(LoomRootCause))]
[JsonSerializable(typeof(LoomSolution))]
[JsonSerializable(typeof(LoomExploreRequest))]
internal partial class LoomInsightJsonContext : JsonSerializerContext;
