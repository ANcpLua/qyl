// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow.Executors;

internal sealed class ContextExecutor(
    string id,
    AIAgent agent,
    AutofixContextLoader loader,
    AutofixReportAssemblyState state,
    IAutofixStepLedger ledger)
    : Executor<FixabilityVerdict, ContextSummary>(id)
{
    public override async ValueTask<ContextSummary> HandleAsync(
        FixabilityVerdict verdict, IWorkflowContext _, CancellationToken ct = default)
    {
        var loaded = await loader.LoadAsync(verdict.RunId, ct).ConfigureAwait(false);

        var prompt = $"""
                      Fixability decision: {verdict.Decision} (score {verdict.Score}/5)
                      Missing signal: {verdict.MissingSignal ?? "(none)"}

                      ## Issue
                      {loaded.IssueBlock}

                      ## Recent events
                      {loaded.EventsBlock}
                      """;

        var response = await agent
            .RunAsync<ContextSummaryDraft>(prompt, cancellationToken: ct)
            .ConfigureAwait(false);
        var draft = response.Result;

        var summary = new ContextSummary(
            verdict.RunId,
            draft.Summary,
            draft.SignalsFound ?? [],
            draft.SignalsAbsent ?? []);

        state.Record(verdict.RunId, summary);
        await ledger.RecordContextAsync(summary, ct).ConfigureAwait(false);

        return summary;
    }

    private sealed record ContextSummaryDraft(
        string Summary,
        IReadOnlyList<string>? SignalsFound,
        IReadOnlyList<string>? SignalsAbsent);
}
