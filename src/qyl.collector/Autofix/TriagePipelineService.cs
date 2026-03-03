using Microsoft.Extensions.AI;

namespace qyl.collector.Autofix;

/// <summary>
///     Background service that periodically scans for untriaged error issues,
///     scores their fixability (via LLM or heuristic fallback), generates
///     summaries, and routes high-confidence issues to the autofix pipeline.
/// </summary>
public sealed partial class TriagePipelineService(
    DuckDbStore store,
    AutofixOrchestrator orchestrator,
    IConfiguration configuration,
    ILogger<TriagePipelineService> logger,
    IChatClient? llm = null)
    : BackgroundService
{
    private readonly bool _enabled = configuration.GetValue("QYL_TRIAGE_ENABLED", true);
    private readonly int _intervalSeconds = configuration.GetValue("QYL_TRIAGE_INTERVAL_SECONDS", 30);
    private readonly double _autoThreshold = configuration.GetValue("QYL_TRIAGE_AUTO_THRESHOLD", 0.8);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            LogTriageDisabled();
            return;
        }

        // Warmup delay — let ingestion pipeline settle
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
        LogTriageStarted(_intervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await TriageUntriagedIssuesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTriageError(ex);
            }
        }
    }

    internal async Task TriageUntriagedIssuesAsync(CancellationToken ct)
    {
        IReadOnlyList<string> issueIds = await store.GetUntriagedIssueIdsAsync(20, ct).ConfigureAwait(false);
        if (issueIds.Count == 0) return;

        LogTriageBatchStart(issueIds.Count);

        foreach (string issueId in issueIds)
        {
            IssueSummary? issue = await store.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
            if (issue is null) continue;

            TriageResult result = llm is not null
                ? await ScoreWithLlmAsync(issue, ct).ConfigureAwait(false)
                : ScoreWithHeuristic(issue);

            await store.InsertTriageResultAsync(result, ct).ConfigureAwait(false);
            LogIssueTriaged(issueId, result.FixabilityScore, result.AutomationLevel);

            // Route auto-fixable issues to the autofix pipeline
            if (result.FixabilityScore >= _autoThreshold)
            {
                FixRunRecord run = await orchestrator.CreateFixRunAsync(
                    issueId, issue, FixPolicy.AutoApply, ct).ConfigureAwait(false);
                await store.UpdateTriageFixRunAsync(result.TriageId, run.RunId, ct).ConfigureAwait(false);
                LogAutoRouted(issueId, run.RunId);
            }
        }
    }

    private async Task<TriageResult> ScoreWithLlmAsync(IssueSummary issue, CancellationToken ct)
    {
        string prompt = $"""
                         {TriagePrompts.FixabilityScoring}
                         Type: {issue.ErrorType}
                         Message: {issue.ErrorMessage ?? "N/A"}
                         Occurrences: {issue.EventCount}
                         First seen: {issue.FirstSeen:O}
                         Last seen: {issue.LastSeen:O}
                         Status: {issue.Status}
                         """;

        try
        {
            ChatResponse response = await llm!.GetResponseAsync(prompt, cancellationToken: ct)
                .ConfigureAwait(false);

            string text = response.Text ?? "";
            LlmTriageResponse? parsed = TryParseResponse(text);

            if (parsed is not null)
            {
                return new TriageResult
                {
                    TriageId = Guid.NewGuid().ToString("N"),
                    IssueId = issue.IssueId,
                    FixabilityScore = Math.Clamp(parsed.FixabilityScore, 0.0, 1.0),
                    AutomationLevel = parsed.AutomationLevel ?? DeriveAutomationLevel(parsed.FixabilityScore),
                    AiSummary = parsed.Summary,
                    RootCauseHypothesis = parsed.RootCauseHypothesis,
                    TriggeredBy = "new_issue",
                    ScoringMethod = "llm"
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLlmScoringFailed(issue.IssueId, ex);
        }

        // Fallback to heuristic if LLM fails
        return ScoreWithHeuristic(issue);
    }

    internal static TriageResult ScoreWithHeuristic(IssueSummary issue)
    {
        // Heuristic scoring based on error characteristics
        double score = 0.3; // Base score

        // High occurrence count suggests reproducible → more fixable
        if (issue.EventCount >= 10) score += 0.15;
        else if (issue.EventCount >= 3) score += 0.1;

        // Known error types are more fixable
        if (issue.ErrorType.ContainsIgnoreCase("NullReference") ||
            issue.ErrorType.ContainsIgnoreCase("ArgumentException") ||
            issue.ErrorType.ContainsIgnoreCase("InvalidOperation"))
            score += 0.2;

        // Recent errors are more actionable
        TimeSpan age = TimeProvider.System.GetUtcNow().UtcDateTime - issue.LastSeen;
        if (age < TimeSpan.FromHours(1)) score += 0.1;
        else if (age < TimeSpan.FromDays(1)) score += 0.05;

        score = Math.Clamp(score, 0.0, 1.0);

        return new TriageResult
        {
            TriageId = Guid.NewGuid().ToString("N"),
            IssueId = issue.IssueId,
            FixabilityScore = score,
            AutomationLevel = DeriveAutomationLevel(score),
            TriggeredBy = "new_issue",
            ScoringMethod = "heuristic"
        };
    }

    internal static string DeriveAutomationLevel(double score) =>
        score switch
        {
            >= 0.8 => "auto",
            >= 0.5 => "assisted",
            >= 0.2 => "manual",
            _ => "skip"
        };

    private static LlmTriageResponse? TryParseResponse(string text)
    {
        // Extract JSON from potential markdown code blocks
        int jsonStart = text.IndexOf('{');
        int jsonEnd = text.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart) return null;

        ReadOnlySpan<char> json = text.AsSpan(jsonStart, jsonEnd - jsonStart + 1);
        try
        {
            return JsonSerializer.Deserialize(json, TriageJsonContext.Default.LlmTriageResponse);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // LoggerMessage source-generated log methods
    [LoggerMessage(Level = LogLevel.Information, Message = "Triage pipeline disabled via QYL_TRIAGE_ENABLED=false")]
    private partial void LogTriageDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Triage pipeline started (interval: {IntervalSeconds}s)")]
    private partial void LogTriageStarted(int intervalSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Triage pipeline error")]
    private partial void LogTriageError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Triaging {Count} untriaged issues")]
    private partial void LogTriageBatchStart(int count);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Issue {IssueId} triaged: score={Score:F2}, level={Level}")]
    private partial void LogIssueTriaged(string issueId, double score, string level);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Issue {IssueId} auto-routed to fix run {RunId}")]
    private partial void LogAutoRouted(string issueId, string runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM scoring failed for issue {IssueId}, falling back to heuristic")]
    private partial void LogLlmScoringFailed(string issueId, Exception ex);
}

/// <summary>JSON response shape from the LLM fixability scoring prompt.</summary>
internal sealed record LlmTriageResponse
{
    [JsonPropertyName("fixability_score")]
    public double FixabilityScore { get; init; }

    [JsonPropertyName("automation_level")]
    public string? AutomationLevel { get; init; }

    [JsonPropertyName("root_cause_hypothesis")]
    public string? RootCauseHypothesis { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }
}

[JsonSerializable(typeof(LlmTriageResponse))]
internal sealed partial class TriageJsonContext : JsonSerializerContext;
