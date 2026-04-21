// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI.Workflows;

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// <summary>
///     Entry executor. Receives a <see cref="StartAutofix" /> with just a run id, loads the
///     <see cref="FixRunRecord" /> and <see cref="IssueSummary" /> from the collector, marks the run as
///     <c>running</c>, and emits the initial <see cref="AutofixRunState" />. Missing run or missing issue
///     is a fatal error that short-circuits the pipeline to the terminal policy gate.
/// </summary>
internal sealed class StartAutofixExecutor(
    CollectorClient collector,
    AutofixOrchestrator orchestrator)
    : Executor<StartAutofix, AutofixRunState>("autofix.start")
{
    public override async ValueTask<AutofixRunState> HandleAsync(
        StartAutofix message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var pending = await collector.GetPendingFixRunsAsync(50, cancellationToken).ConfigureAwait(false);
        var run = pending.FirstOrDefault(r => r.RunId == message.RunId);

        if (run is null)
        {
            return new AutofixRunState
            {
                RunId = message.RunId,
                IssueId = string.Empty,
                Policy = FixPolicy.RequireReview,
                IsFatalError = true,
                FatalErrorMessage = $"Fix run '{message.RunId}' not found among pending runs.",
            };
        }

        await orchestrator
            .UpdateFixRunStatusAsync(run.IssueId, run.RunId, "running", ct: cancellationToken)
            .ConfigureAwait(false);

        var policy = Enum.TryParse<FixPolicy>(run.Policy, ignoreCase: true, out var parsed)
            ? parsed
            : FixPolicy.RequireReview;

        var issue = await collector.GetIssueByIdAsync(run.IssueId, cancellationToken).ConfigureAwait(false);

        if (issue is null)
        {
            return new AutofixRunState
            {
                RunId = run.RunId,
                IssueId = run.IssueId,
                Policy = policy,
                Instruction = run.Instruction,
                StoppingPoint = run.StoppingPoint,
                IsFatalError = true,
                FatalErrorMessage = "Issue not found",
            };
        }

        return new AutofixRunState
        {
            RunId = run.RunId,
            IssueId = run.IssueId,
            Policy = policy,
            Instruction = run.Instruction,
            StoppingPoint = run.StoppingPoint,
            Issue = issue,
        };
    }
}
