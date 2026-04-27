// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow.Executors;

internal sealed class HypothesisExecutor(
    string id,
    AIAgent agent,
    AutofixWorkflowConfig config,
    AutofixReportAssemblyState state,
    IAutofixStepLedger ledger)
    : Executor<ContextSummary, HypothesisVerdict>(id)
{
    public override async ValueTask<HypothesisVerdict> HandleAsync(
        ContextSummary context, IWorkflowContext _, CancellationToken ct = default)
    {
        var found = string.Join("\n", context.SignalsFound.Select(static s => $"- {s}"));
        var absent = string.Join("\n", context.SignalsAbsent.Select(static s => $"- {s}"));

        var prompt = $"""
                      Generate {config.HypothesisFanOut} distinct candidate root-cause hypotheses for this
                      issue, each from a different angle (concurrency / data shape / config drift / network /
                      build / deploy). Then pick the strongest as primary, optionally rank one as alternative,
                      and explain your judge rationale.

                      ## Context summary
                      {context.Summary}

                      ## Signals found
                      {found}

                      ## Signals absent
                      {absent}
                      """;

        var response = await agent
            .RunAsync<HypothesisVerdictDraft>(prompt, cancellationToken: ct)
            .ConfigureAwait(false);
        var draft = response.Result;

        var verdict = new HypothesisVerdict(
            context.RunId,
            draft.Primary,
            draft.Alternative,
            draft.JudgeRationale ?? "(no rationale)",
            RetryIteration: 0);

        state.Record(context.RunId, verdict);
        await ledger.RecordHypothesisAsync(verdict, ct).ConfigureAwait(false);

        return verdict;
    }

    private sealed record HypothesisVerdictDraft(
        string Primary,
        string? Alternative,
        string? JudgeRationale,
        IReadOnlyList<string>? CandidatesConsidered);
}
