// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow.Executors;

internal sealed class FixabilityExecutor(
    string id,
    AIAgent agent,
    AutofixReportAssemblyState state,
    IAutofixStepLedger ledger)
    : Executor<AutofixWorkflowRequest, FixabilityVerdict>(id)
{
    public override async ValueTask<FixabilityVerdict> HandleAsync(
        AutofixWorkflowRequest request, IWorkflowContext ctx, CancellationToken ct = default)
    {
        var prompt = $"""
                      Issue id: {request.IssueId}
                      Run id:   {request.RunId}
                      Policy:   {request.Policy}
                      Caller instruction (untrusted): {request.CallerInstruction ?? "(none)"}
                      """;

        var response = await agent.RunAsync<FixabilityVerdictDraft>(prompt, cancellationToken: ct).ConfigureAwait(false);
        var draft = response.Result;

        var verdict = new FixabilityVerdict(
            request.RunId,
            Math.Clamp(draft.Score, 0, 5),
            draft.Decision is "continue" or "need_more_signal" ? draft.Decision : "need_more_signal",
            draft.MissingSignal);

        state.Record(request.RunId, verdict);
        await ledger.RecordFixabilityAsync(verdict, ct).ConfigureAwait(false);
        await ctx.AddEventAsync(new FixabilityRecorded(verdict), ct).ConfigureAwait(false);

        return verdict;
    }

    private sealed record FixabilityVerdictDraft(int Score, string Decision, string? MissingSignal);
}
