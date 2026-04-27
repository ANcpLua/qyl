// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// Back-edge router for the self-critique loop. Fires when ConfidenceAudit
/// signals retry. Reads the original ContextSummary from state, augments it
/// with the failed hypothesis + audit so the next fan-out generation tries a
/// different angle, and forwards the augmented ContextSummary into the
/// hypothesis branches via the second AddFanOutEdge in the workflow.
internal sealed class SelfCritiqueRouter(string id, AutofixReportAssemblyState state)
    : Executor<ConfidenceAudit, ContextSummary>(id)
{
    public override ValueTask<ContextSummary> HandleAsync(
        ConfidenceAudit audit, IWorkflowContext ctx, CancellationToken ct = default)
    {
        var snapshot = state.Snapshot(audit.RunId);
        var original = snapshot.Context
                       ?? new ContextSummary(audit.RunId, "(no prior context)", [], []);
        var priorHypothesis = snapshot.Hypothesis?.Primary ?? "(no prior hypothesis)";

        var augmented = original with
        {
            Summary = $"""
                       {original.Summary}

                       ## Self-critique feedback (retry pass)
                       Prior hypothesis ("{priorHypothesis}") scored confidence {audit.Level}
                       ({audit.ScoreSum}/12). Pick a different angle. Cite different signals.
                       """
        };

        return ValueTask.FromResult(augmented);
    }
}
