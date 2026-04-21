// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI.Workflows;

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// <summary>
///     Terminal executor. Observes the accumulated <see cref="AutofixRunState" /> and transitions the run to its
///     final status via <see cref="AutofixOrchestrator" />: <c>failed</c> for infrastructure errors, <c>review</c>
///     for early stops, or the policy-gate decision for the full pipeline. Yields the state so the workflow
///     completes.
/// </summary>
internal sealed partial class PolicyGateExecutor(
    AutofixOrchestrator orchestrator,
    ILogger<PolicyGateExecutor> logger)
    : Executor<AutofixRunState>("autofix.policy_gate")
{
    public override async ValueTask HandleAsync(
        AutofixRunState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (state.IsFatalError)
        {
            await orchestrator
                .UpdateFixRunStatusAsync(
                    state.IssueId, state.RunId, "failed",
                    state.FatalErrorMessage, ct: cancellationToken)
                .ConfigureAwait(false);
            LogFixRunFailed(state.RunId, state.FatalErrorMessage ?? "unknown");
        }
        else if (state.IsEarlyStop)
        {
            await orchestrator
                .UpdateFixRunStatusAsync(
                    state.IssueId, state.RunId, "review",
                    state.EarlyStopReason, changesJson: state.ChangesJson, ct: cancellationToken)
                .ConfigureAwait(false);
            LogFixRunStoppedEarly(state.RunId, state.StoppingPoint ?? "unknown");
        }
        else
        {
            var confidence = state.Confidence
                             ?? throw new InvalidOperationException("ConfidenceExecutor must run before PolicyGate.");
            var nextStatus = PolicyGate.EvaluateNextStatus(state.Policy, confidence.Confidence);
            var description =
                $"Autofix pipeline complete | confidence={confidence.Confidence:F2} | {confidence.Recommendation}";

            await orchestrator
                .UpdateFixRunStatusAsync(
                    state.IssueId, state.RunId, nextStatus, description,
                    confidence.Confidence, state.ChangesJson, cancellationToken)
                .ConfigureAwait(false);
            LogFixRunCompleted(state.RunId, nextStatus, confidence.Confidence);
        }

        await context.YieldOutputAsync(state, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fix run {RunId} completed: status={Status}, confidence={Confidence:F2}")]
    private partial void LogFixRunCompleted(string runId, string status, double confidence);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fix run {RunId} stopped early at {StoppingPoint}")]
    private partial void LogFixRunStoppedEarly(string runId, string stoppingPoint);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Fix run {RunId} failed: {Message}")]
    private partial void LogFixRunFailed(string runId, string message);
}
