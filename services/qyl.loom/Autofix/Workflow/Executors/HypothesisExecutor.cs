
namespace Qyl.Loom.Autofix.Workflow.Executors;

internal sealed class HypothesisExecutor(
    string id,
    string perspective,
    AIAgent agent,
    IAutofixStepLedger ledger)
    : Executor<ContextSummary, HypothesisCandidate>(id)
{
    public override async ValueTask<HypothesisCandidate> HandleAsync(
        ContextSummary context, IWorkflowContext ctx, CancellationToken ct = default)
    {
        var found = string.Join("\n", context.SignalsFound.Select(static s => $"- {s}"));
        var absent = string.Join("\n", context.SignalsAbsent.Select(static s => $"- {s}"));

        var prompt = $"""
                      Branch perspective: {perspective}

                      Propose ONE primary root-cause hypothesis for this issue from the
                      "{perspective}" lens. Optionally rank one alternative. Every claim must
                      cite a signal from the context. Self-report your confidence 0..1.

                      ## Context summary
                      {context.Summary}

                      ## Signals found
                      {found}

                      ## Signals absent
                      {absent}
                      """;

        var response = await agent
            .RunAsync<HypothesisCandidateDraft>(prompt, cancellationToken: ct)
            .ConfigureAwait(false);
        var draft = response.Result;

        var candidate = new HypothesisCandidate(
            context.RunId,
            perspective,
            draft.Primary,
            draft.Alternative,
            Math.Clamp(draft.SelfReportedConfidence, 0.0, 1.0));

        await ledger.RecordHypothesisCandidateAsync(candidate, ct).ConfigureAwait(false);
        await ctx.AddEventAsync(new HypothesisCandidateRecorded(candidate), ct).ConfigureAwait(false);

        return candidate;
    }

    private sealed record HypothesisCandidateDraft(
        string Primary,
        string? Alternative,
        double SelfReportedConfidence);
}
