// Copyright (c) 2025-2026 ancplua

using Qyl.Loom.Agents;
using Qyl.Loom.Autofix.Workflow;
using Qyl.Loom.Workflows;

namespace Qyl.Loom.Autofix;

internal sealed partial class LoomAutofixRunner(
    CollectorClient collector,
    AutofixOrchestrator orchestrator,
    ILogger<LoomAutofixRunner> logger,
    IQylLoomAgentsBuilder agents,
    IQylLoomWorkflowBuilder workflows,
    AutofixRunRegistry registry,
    AutofixReportAssemblyState assembly,
    IAutofixLifecycleBus lifecycle)
{
    public Task RunAsync(string runId, CancellationToken ct = default) =>
        RunAsync(runId, AutofixWorkflowDefaults.Config, ct);

    public async Task RunAsync(string runId, AutofixWorkflowConfig config, CancellationToken ct = default)
    {
        if (!agents.IsConfigured)
        {
            LogNoLlmConfigured(runId);
            return;
        }

        var run = await collector.GetFixRunAsync(runId, ct).ConfigureAwait(false);
        if (run is null)
        {
            LogRunNotFound(runId);
            return;
        }

        var policy = Enum.TryParse<FixPolicy>(run.Policy, true, out var parsedPolicy)
            ? parsedPolicy
            : FixPolicy.RequireReview;

        var issue = await collector.GetIssueByIdAsync(run.IssueId, ct).ConfigureAwait(false);
        if (issue is null)
        {
            await orchestrator
                .UpdateFixRunStatusAsync(run.IssueId, runId, "failed",
                    $"Issue '{run.IssueId}' not found.", ct: ct)
                .ConfigureAwait(false);
            LogIssueNotFound(runId, run.IssueId);
            return;
        }

        await orchestrator.UpdateFixRunStatusAsync(run.IssueId, runId, "running", ct: ct).ConfigureAwait(false);

        registry.Register(new AutofixRunRegistry.RegisteredRun(runId, run.IssueId, run.Policy));

        try
        {
            var report = await ExecuteWorkflowAsync(run, config, ct).ConfigureAwait(false);

            var confidence = report.ConfidenceScoreSum / 12.0;
            var nextStatus = PolicyGate.EvaluateNextStatus(policy, confidence);
            var description =
                $"Loom autofix complete | confidence={confidence:F2} ({report.ConfidenceLevel}) | fixability={report.FixabilityScore}/5";
            var changesJson = BuildChangesJson(report);

            await orchestrator
                .UpdateFixRunStatusAsync(run.IssueId, runId, nextStatus, description,
                    confidence, changesJson, ct)
                .ConfigureAwait(false);

            LogRunCompleted(runId, nextStatus, confidence);
        }
        catch (JsonException ex)
        {
            await orchestrator
                .UpdateFixRunStatusAsync(run.IssueId, runId, "failed",
                    $"Structured-output schema violation: {ex.Message}", ct: ct)
                .ConfigureAwait(false);
            LogSchemaViolation(runId, ex);
        }
        catch (HttpRequestException ex)
        {
            await orchestrator
                .UpdateFixRunStatusAsync(run.IssueId, runId, "failed",
                    $"Transport failure: {ex.Message}", ct: ct)
                .ConfigureAwait(false);
            LogTransportFailure(runId, ex);
        }
        finally
        {
            registry.TryRemove(runId);
            assembly.TryRemove(runId);
        }
    }

    private async Task<AutofixReport> ExecuteWorkflowAsync(
        FixRunRecord run, AutofixWorkflowConfig config, CancellationToken ct)
    {
        var workflow = workflows.BuildAutofixWorkflow(config);
        var request = new AutofixWorkflowRequest(
            run.RunId,
            run.IssueId,
            run.Policy,
            run.Instruction,
            run.StoppingPoint,
            config);

        await using var execution = await InProcessExecution
            .RunStreamingAsync(workflow, request, CheckpointManager.Default, run.RunId, ct)
            .ConfigureAwait(false);

        AutofixWorkflowResult? final = null;

        try
        {
            await foreach (var evt in execution.WatchStreamAsync(ct).ConfigureAwait(false))
            {
                switch (evt)
                {
                    case AutofixLifecycleEvent lifecycleEvent:
                        lifecycle.Publish(run.RunId, ToEnvelope(lifecycleEvent));
                        break;

                    case WorkflowOutputEvent { Data: AutofixWorkflowResult result }:
                        final = result;
                        break;
                }

                if (final is not null) break;
            }
        }
        finally
        {
            lifecycle.Complete(run.RunId);
        }

        return final?.Report
               ?? throw new InvalidOperationException(
                   $"Autofix workflow for run {run.RunId} terminated without emitting AutofixWorkflowResult.");
    }

    private static AutofixLifecycleEnvelope ToEnvelope(AutofixLifecycleEvent evt) =>
        new(
            evt.RunId,
            evt.Stage,
            evt.GetType().Name,
            JsonSerializer.Serialize<object>(evt.Data),
            DateTimeOffset.UtcNow);

    private static string BuildChangesJson(AutofixReport report) =>
        JsonSerializer.Serialize(new
        {
            repo = report.SolutionRepo,
            diff = report.SolutionDiff,
            regression_test = report.RegressionTest,
            primary_hypothesis = report.PrimaryHypothesis,
            confidence_level = report.ConfidenceLevel,
            final_report = report.FinalReport
        });

    [LoggerMessage(Level = LogLevel.Information, Message = "Loom autofix skipped for run {RunId}: no LLM configured")]
    private partial void LogNoLlmConfigured(string runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loom autofix run {RunId} not found in collector")]
    private partial void LogRunNotFound(string runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loom autofix run {RunId}: issue {IssueId} not found")]
    private partial void LogIssueNotFound(string runId, string issueId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Loom autofix run {RunId} completed: status={Status}, confidence={Confidence:F2}")]
    private partial void LogRunCompleted(string runId, string status, double confidence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loom autofix run {RunId} schema violation")]
    private partial void LogSchemaViolation(string runId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loom autofix run {RunId} transport failure")]
    private partial void LogTransportFailure(string runId, Exception ex);
}

internal static class AutofixWorkflowDefaults
{
    public static readonly AutofixWorkflowConfig Config = new(
        HypothesisFanOut: 3,
        HypothesisTemperatureSpread: 0.4,
        HypothesisAlternateModel: null,
        ConfidenceRetryThreshold: 9,
        MaxConfidenceRetries: 1,
        StoppingPointAfterHypothesis: false,
        StoppingPointBeforeCommit: false,
        ToolUsingContext: false,
        ContextToolBudget: 6);
}
