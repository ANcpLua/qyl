
namespace Qyl.Loom.Autofix.Workflow.Executors;

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
