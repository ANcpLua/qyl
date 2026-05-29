using Qyl.Contracts.Observability;
using Qyl.Loom.Agents;
using Qyl.Loom.Autofix;

namespace Qyl.Loom;

[QylHostedService]
public sealed partial class TriagePipelineService(
    CollectorClient collector,
    AutofixOrchestrator orchestrator,
    IConfiguration configuration,
    ILogger<TriagePipelineService> logger,
    IQylLoomAgentsBuilder agents)
    : BackgroundService
{
    private readonly double _autoThreshold = configuration.GetValue("QYL_TRIAGE_AUTO_THRESHOLD", 0.8);
    private readonly bool _enabled = configuration.GetValue("QYL_TRIAGE_ENABLED", true);
    private readonly int _intervalSeconds = configuration.GetValue("QYL_TRIAGE_INTERVAL_SECONDS", 30);
    private readonly int _maxParallelism = Math.Clamp(
        configuration.GetValue("QYL_TRIAGE_MAX_PARALLELISM", 4), 1, 16);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            LogTriageDisabled();
            return;
        }

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

    internal async Task<TriageResult?> TriageSingleIssueAsync(string issueId, CancellationToken ct)
    {
        var issue = await collector.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        if (issue is null) return null;

        var result = agents.IsConfigured
            ? await ScoreWithLlmAsync(issue, ct).ConfigureAwait(false)
            : ScoreWithHeuristic(issue);

        await collector.InsertTriageResultAsync(result, ct).ConfigureAwait(false);
        LogIssueTriaged(issueId, result.FixabilityScore, result.AutomationLevel);

        if (result.FixabilityScore >= _autoThreshold)
        {
            var run = await orchestrator.CreateFixRunAsync(
                issueId, FixPolicy.AutoApply, ct: ct).ConfigureAwait(false);
            await collector.UpdateTriageFixRunAsync(result.TriageId, run.RunId, ct).ConfigureAwait(false);
            LogAutoRouted(issueId, run.RunId);
        }

        return result;
    }

    internal async Task TriageUntriagedIssuesAsync(CancellationToken ct)
    {
        var issueIds = await collector.GetUntriagedIssueIdsAsync(20, ct).ConfigureAwait(false);
        if (issueIds.Count is 0) return;

        LogTriageBatchStart(issueIds.Count);

        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxParallelism, CancellationToken = ct };
        await Parallel.ForEachAsync(issueIds, options, async (issueId, perItemCt) =>
        {
            var issue = await collector.GetIssueByIdAsync(issueId, perItemCt).ConfigureAwait(false);
            if (issue is null) return;

            var result = agents.IsConfigured
                ? await ScoreWithLlmAsync(issue, perItemCt).ConfigureAwait(false)
                : ScoreWithHeuristic(issue);

            await collector.InsertTriageResultAsync(result, perItemCt).ConfigureAwait(false);
            LogIssueTriaged(issueId, result.FixabilityScore, result.AutomationLevel);

            if (result.FixabilityScore >= _autoThreshold)
            {
                var run = await orchestrator.CreateFixRunAsync(
                    issueId, FixPolicy.AutoApply, ct: perItemCt).ConfigureAwait(false);
                await collector.UpdateTriageFixRunAsync(result.TriageId, run.RunId, perItemCt)
                    .ConfigureAwait(false);
                LogAutoRouted(issueId, run.RunId);
            }
        }).ConfigureAwait(false);
    }

    private async Task<TriageResult> ScoreWithLlmAsync(IssueSummary issue, CancellationToken ct)
    {
        var userMessage = $"""
                           Type: {issue.ErrorType}
                           Message: {issue.ErrorMessage ?? "N/A"}
                           Occurrences: {issue.EventCount}
                           First seen: {issue.FirstSeen:O}
                           Last seen: {issue.LastSeen:O}
                           Status: {issue.Status}
                           """;

        var agent = agents.BuildTriageScoringAgent();

        try
        {
            var response = await agent.RunAsync<LlmTriageResponse>(
                userMessage,
                serializerOptions: TriageJsonContext.Default.Options,
                cancellationToken: ct).ConfigureAwait(false);

            if (response.Result is { } parsed)
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

        return ScoreWithHeuristic(issue);
    }

    internal static TriageResult ScoreWithHeuristic(IssueSummary issue)
    {
        var score = 0.3;

        switch (issue.EventCount)
        {
            case >= 10:
                score += 0.15;
                break;
            case >= 3:
                score += 0.1;
                break;
        }

        if (issue.ErrorType.ContainsIgnoreCase("NullReference") ||
            issue.ErrorType.ContainsIgnoreCase("ArgumentException") ||
            issue.ErrorType.ContainsIgnoreCase("InvalidOperation"))
            score += 0.2;

        var age = TimeProvider.System.GetUtcNow().UtcDateTime - issue.LastSeen;
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

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "LLM scoring failed for issue {IssueId}, falling back to heuristic")]
    private partial void LogLlmScoringFailed(string issueId, Exception ex);
}
