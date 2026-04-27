// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow.Executors;

internal sealed class SolutionExecutor(
    string id,
    AIAgent agent,
    AutofixReportAssemblyState state,
    IAutofixStepLedger ledger)
    : Executor<HypothesisVerdict, SolutionDraft>(id)
{
    public override async ValueTask<SolutionDraft> HandleAsync(
        HypothesisVerdict verdict, IWorkflowContext ctx, CancellationToken ct = default)
    {
        var prompt = $"""
                      ## Chosen hypothesis (iteration {verdict.RetryIteration})
                      Primary: {verdict.Primary}
                      Alternative: {verdict.Alternative ?? "(none)"}

                      Judge rationale: {verdict.JudgeRationale}

                      Produce the minimal patch.
                      """;

        var response = await agent
            .RunAsync<SolutionDraftRecord>(prompt, cancellationToken: ct)
            .ConfigureAwait(false);
        var d = response.Result;

        var draft = new SolutionDraft(verdict.RunId, d.Repo, d.Diff, d.RegressionTest);

        state.Record(verdict.RunId, draft);
        await ledger.RecordSolutionAsync(draft, ct).ConfigureAwait(false);
        await ctx.AddEventAsync(new SolutionRecorded(draft), ct).ConfigureAwait(false);

        return draft;
    }

    private sealed record SolutionDraftRecord(string? Repo, string? Diff, string? RegressionTest);
}
