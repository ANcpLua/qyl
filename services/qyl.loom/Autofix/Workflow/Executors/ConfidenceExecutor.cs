// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow.Executors;

internal sealed class ConfidenceExecutor(
    string id,
    AIAgent agent,
    AutofixWorkflowConfig config,
    AutofixReportAssemblyState state,
    IAutofixStepLedger ledger)
    : Executor<SolutionDraft, ConfidenceAudit>(id)
{
    public override async ValueTask<ConfidenceAudit> HandleAsync(
        SolutionDraft draft, IWorkflowContext ctx, CancellationToken ct = default)
    {
        var prompt = $"""
                      ## Solution under audit
                      Repo: {draft.Repo ?? "(none)"}
                      Diff length: {draft.Diff?.Length ?? 0} chars
                      Regression test: {(draft.RegressionTest is null ? "(missing)" : "present")}

                      Score the four gates and emit ConfidenceAudit. Set retry_requested true when the gate
                      sum is below {config.ConfidenceRetryThreshold}.
                      """;

        var response = await agent
            .RunAsync<ConfidenceAuditDraft>(prompt, cancellationToken: ct)
            .ConfigureAwait(false);
        var d = response.Result;

        var sum = Math.Clamp(d.EvidenceGate, 0, 3)
                  + Math.Clamp(d.RegressionGate, 0, 3)
                  + Math.Clamp(d.CompletenessGate, 0, 3)
                  + Math.Clamp(d.SelfChallengeGate, 0, 3);

        var level = sum switch
        {
            >= 9 => "high",
            >= 6 => "medium",
            _ => "low"
        };

        var audit = new ConfidenceAudit(
            draft.RunId,
            level,
            sum,
            Math.Clamp(d.EvidenceGate, 0, 3),
            Math.Clamp(d.RegressionGate, 0, 3),
            Math.Clamp(d.CompletenessGate, 0, 3),
            Math.Clamp(d.SelfChallengeGate, 0, 3),
            d.RetryRequested && sum < config.ConfidenceRetryThreshold);

        state.Record(draft.RunId, audit);
        await ledger.RecordConfidenceAsync(audit, ct).ConfigureAwait(false);
        await ctx.AddEventAsync(new ConfidenceRecorded(audit), ct).ConfigureAwait(false);

        return audit;
    }

    private sealed record ConfidenceAuditDraft(
        int EvidenceGate,
        int RegressionGate,
        int CompletenessGate,
        int SelfChallengeGate,
        bool RetryRequested);
}
