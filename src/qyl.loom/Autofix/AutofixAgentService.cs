using Microsoft.Extensions.AI;

namespace Qyl.Loom;

/// <summary>
///     Background service that autonomously picks up pending fix runs and executes
///     the multi-step autofix pipeline: gather context -> RCA -> solution plan ->
///     diff generation -> confidence scoring -> policy gate routing.
/// </summary>
public sealed partial class AutofixAgentService(
    CollectorClient collector,
    AutofixOrchestrator orchestrator,
    IConfiguration configuration,
    ILogger<AutofixAgentService> logger,
    IChatClient? llm = null)
    : BackgroundService
{
    private readonly bool _enabled = configuration.GetValue("QYL_AUTOFIX_ENABLED", true);
    private readonly int _intervalSeconds = configuration.GetValue("QYL_AUTOFIX_INTERVAL_SECONDS", 15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled || llm is null)
        {
            LogAutofixDisabled(llm is null ? "no LLM configured" : "QYL_AUTOFIX_ENABLED=false");
            return;
        }

        // Warmup delay — let triage pipeline populate pending fix runs
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);
        LogAutofixStarted(_intervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await ProcessPendingFixRunsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogAutofixError(ex);
            }
        }
    }

    internal async Task ProcessPendingFixRunsAsync(CancellationToken ct)
    {
        var pending = await collector.GetPendingFixRunsAsync(5, ct)
            .ConfigureAwait(false);

        if (pending.Count == 0) return;

        LogProcessingBatch(pending.Count);

        foreach (var run in pending)
        {
            await ProcessFixRunAsync(run, ct).ConfigureAwait(false);
        }
    }

    private async Task ProcessFixRunAsync(FixRunRecord run, CancellationToken ct)
    {
        // Mark as running
        await orchestrator.UpdateFixRunStatusAsync(run.IssueId, run.RunId, "running", ct: ct)
            .ConfigureAwait(false);

        try
        {
            // ── Step 1: Gather Context ──────────────────────────────────────
            var stepId1 = await CreateAndStartStepAsync(run.RunId, 1, "gather_context", ct)
                .ConfigureAwait(false);

            var issue = await collector.GetIssueByIdAsync(run.IssueId, ct).ConfigureAwait(false);
            if (issue is null)
            {
                await FailStepAsync(run.RunId, stepId1, "Issue not found", ct).ConfigureAwait(false);
                await orchestrator.UpdateFixRunStatusAsync(run.IssueId, run.RunId, "failed",
                    "Issue not found", ct: ct).ConfigureAwait(false);
                return;
            }

            var contextJson = await GatherContextAsync(run.IssueId, issue, ct).ConfigureAwait(false);
            await CompleteStepAsync(run.RunId, stepId1, contextJson, ct).ConfigureAwait(false);

            // ── Step 2: Root Cause Analysis ─────────────────────────────────
            var stepId2 = await CreateAndStartStepAsync(run.RunId, 2, "root_cause_analysis", ct)
                .ConfigureAwait(false);

            var rcaReport = await RunRcaAsync(issue, contextJson, run.Instruction, ct).ConfigureAwait(false);
            await CompleteStepAsync(run.RunId, stepId2, rcaReport, ct).ConfigureAwait(false);

            if (run.StoppingPoint is "root_cause")
            {
                await CompleteRunEarlyAsync(run, "Stopped at root_cause per stopping_point", ct)
                    .ConfigureAwait(false);
                return;
            }

            // ── Step 3: Solution Planning ───────────────────────────────────
            var stepId3 = await CreateAndStartStepAsync(run.RunId, 3, "solution_planning", ct)
                .ConfigureAwait(false);

            var solutionPlan = await RunSolutionPlanAsync(rcaReport, ct).ConfigureAwait(false);
            await CompleteStepAsync(run.RunId, stepId3, solutionPlan, ct).ConfigureAwait(false);

            if (run.StoppingPoint is "solution")
            {
                await CompleteRunEarlyAsync(run, "Stopped at solution per stopping_point", ct)
                    .ConfigureAwait(false);
                return;
            }

            // ── Step 4: Diff Generation ─────────────────────────────────────
            var stepId4 = await CreateAndStartStepAsync(run.RunId, 4, "diff_generation", ct)
                .ConfigureAwait(false);

            var changesJson = await RunDiffGenerationAsync(rcaReport, solutionPlan, ct)
                .ConfigureAwait(false);
            await CompleteStepAsync(run.RunId, stepId4, changesJson, ct).ConfigureAwait(false);

            if (run.StoppingPoint is "code_changes")
            {
                await CompleteRunEarlyAsync(run, "Stopped at code_changes per stopping_point",
                    ct, changesJson: changesJson).ConfigureAwait(false);
                return;
            }

            // ── Step 5: Confidence Scoring ──────────────────────────────────
            var stepId5 = await CreateAndStartStepAsync(run.RunId, 5, "confidence_scoring", ct)
                .ConfigureAwait(false);

            var confidence = await RunConfidenceScoringAsync(rcaReport, changesJson, ct)
                .ConfigureAwait(false);
            await CompleteStepAsync(run.RunId, stepId5, JsonSerializer.Serialize(confidence,
                AutofixJsonContext.Default.ConfidenceResult), ct).ConfigureAwait(false);

            // ── Policy Gate ─────────────────────────────────────────────────
            var policy = Enum.TryParse<FixPolicy>(run.Policy, true, out var p)
                ? p
                : FixPolicy.RequireReview;

            var nextStatus = PolicyGate.EvaluateNextStatus(policy, confidence.Confidence);

            var description =
                $"Autofix pipeline complete | confidence={confidence.Confidence:F2} | {confidence.Recommendation}";

            await orchestrator.UpdateFixRunStatusAsync(
                    run.IssueId, run.RunId, nextStatus, description, confidence.Confidence, changesJson, ct)
                .ConfigureAwait(false);

            LogFixRunCompleted(run.RunId, nextStatus, confidence.Confidence);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFixRunFailed(run.RunId, ex);
            await orchestrator.UpdateFixRunStatusAsync(
                run.IssueId, run.RunId, "failed", ex.Message, ct: ct).ConfigureAwait(false);
        }
    }

    private async Task CompleteRunEarlyAsync(
        FixRunRecord run, string description, CancellationToken ct, string? changesJson = null)
    {
        await orchestrator.UpdateFixRunStatusAsync(
            run.IssueId, run.RunId, "review", description, changesJson: changesJson, ct: ct)
            .ConfigureAwait(false);

        LogFixRunStoppedEarly(run.RunId, run.StoppingPoint!);
    }

    // ── Step Helpers ────────────────────────────────────────────────────────

    private async Task<string> CreateAndStartStepAsync(
        string runId, int stepNumber, string stepName, CancellationToken ct)
    {
        var stepId = Guid.NewGuid().ToString("N");
        var step = new AutofixStepRecord
        {
            StepId = stepId,
            RunId = runId,
            StepNumber = stepNumber,
            StepName = stepName,
            Status = "running"
        };

        await collector.InsertAutofixStepAsync(step, ct).ConfigureAwait(false);
        LogStepStarted(runId, stepName, stepNumber);
        return stepId;
    }

    private async Task CompleteStepAsync(string runId, string stepId, string outputJson, CancellationToken ct) =>
        await collector.UpdateAutofixStepAsync(runId, stepId, "completed", outputJson, ct: ct)
            .ConfigureAwait(false);

    private async Task FailStepAsync(string runId, string stepId, string error, CancellationToken ct) =>
        await collector.UpdateAutofixStepAsync(runId, stepId, "failed", errorMessage: error, ct: ct)
            .ConfigureAwait(false);

    // ── LLM Pipeline Steps ─────────────────────────────────────────────────

    private async Task<string> GatherContextAsync(
        string issueId, IssueSummary issue, CancellationToken ct)
    {
        var events = await collector.GetIssueEventsAsync(issueId, 5, ct)
            .ConfigureAwait(false);

        var context = new
        {
            issue_id = issueId,
            error_type = issue.ErrorType,
            error_message = issue.ErrorMessage,
            event_count = issue.EventCount,
            first_seen = issue.FirstSeen.ToString("O"),
            last_seen = issue.LastSeen.ToString("O"),
            events = events.Select(static e => new
            {
                e.Id,
                e.Message,
                e.StackTrace,
                e.Environment,
                timestamp = e.Timestamp.ToString("O")
            })
        };

        return JsonSerializer.Serialize(context);
    }

    private async Task<string> RunRcaAsync(
        IssueSummary issue, string contextJson, string? instruction, CancellationToken ct)
    {
        var instructionBlock = instruction is not null
            ? $"\n\nAdditional context from the requester:\n{instruction}"
            : "";

        var userMessage = $"""
                           Investigate this error:
                           Type: {issue.ErrorType}
                           Message: {issue.ErrorMessage ?? "N/A"}
                           Occurrences: {issue.EventCount}

                           Full context:
                           {contextJson}{instructionBlock}
                           """;

        var response = await llm!.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, AutofixPrompts.RootCauseAnalysis),
                new ChatMessage(ChatRole.User, userMessage)
            ],
            cancellationToken: ct).ConfigureAwait(false);

        return response.Text ?? "Root cause analysis produced no output.";
    }

    private async Task<string> RunSolutionPlanAsync(string rcaReport, CancellationToken ct)
    {
        var response = await llm!.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, AutofixPrompts.SolutionPlanning),
                new ChatMessage(ChatRole.User, rcaReport)
            ],
            cancellationToken: ct).ConfigureAwait(false);

        return ExtractJson(response.Text ?? "{}");
    }

    private async Task<string> RunDiffGenerationAsync(
        string rcaReport, string solutionPlan, CancellationToken ct)
    {
        var userMessage = $"""
                           Root Cause Analysis:
                           {rcaReport}

                           Solution Plan:
                           {solutionPlan}
                           """;

        var response = await llm!.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, AutofixPrompts.DiffGeneration),
                new ChatMessage(ChatRole.User, userMessage)
            ],
            cancellationToken: ct).ConfigureAwait(false);

        return ExtractJson(response.Text ?? "{}");
    }

    private async Task<ConfidenceResult> RunConfidenceScoringAsync(
        string rcaReport, string changesJson, CancellationToken ct)
    {
        var userMessage = $"""
                           Root Cause Analysis:
                           {rcaReport}

                           Proposed Fix:
                           {changesJson}
                           """;

        var response = await llm!.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, AutofixPrompts.ConfidenceScoring),
                new ChatMessage(ChatRole.User, userMessage)
            ],
            cancellationToken: ct).ConfigureAwait(false);

        var json = ExtractJson(response.Text ?? "{}");
        try
        {
            return JsonSerializer.Deserialize(json, AutofixJsonContext.Default.ConfidenceResult)
                   ?? new ConfidenceResult { Confidence = 0.5, Reasoning = "Parse failed", Recommendation = "review" };
        }
        catch (JsonException)
        {
            return new ConfidenceResult { Confidence = 0.5, Reasoning = "Parse failed", Recommendation = "review" };
        }
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return "{}";

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;
            if (depth == 0) return text[start..(i + 1)];
        }

        return "{}";
    }

    // ── Log Methods ─────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Autofix agent service disabled: {Reason}")]
    private partial void LogAutofixDisabled(string reason);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Autofix agent service started (interval: {IntervalSeconds}s)")]
    private partial void LogAutofixStarted(int intervalSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Autofix agent service error")]
    private partial void LogAutofixError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Processing {Count} pending fix runs")]
    private partial void LogProcessingBatch(int count);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Fix run {RunId}: starting step {StepName} ({StepNumber}/5)")]
    private partial void LogStepStarted(string runId, string stepName, int stepNumber);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fix run {RunId} completed: status={Status}, confidence={Confidence:F2}")]
    private partial void LogFixRunCompleted(string runId, string status, double confidence);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Fix run {RunId} failed")]
    private partial void LogFixRunFailed(string runId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fix run {RunId} stopped early at {StoppingPoint}")]
    private partial void LogFixRunStoppedEarly(string runId, string stoppingPoint);
}
