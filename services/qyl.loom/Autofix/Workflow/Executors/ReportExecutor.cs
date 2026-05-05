
namespace Qyl.Loom.Autofix.Workflow.Executors;

[YieldsOutput(typeof(AutofixWorkflowResult))]
internal sealed class ReportExecutor(
    string id,
    AIAgent agent,
    AutofixReportAssemblyState state,
    IAutofixStepLedger ledger)
    : Executor<ConfidenceAudit>(id)
{
    public override async ValueTask HandleAsync(
        ConfidenceAudit audit, IWorkflowContext ctx, CancellationToken ct = default)
    {
        var snapshot = await ResolveSnapshotAsync(audit, ctx, ct).ConfigureAwait(false);

        var prompt = $"""
                      Synthesize a 200-word maximum plain-English handoff.

                      Hypothesis: {snapshot.Hypothesis?.Primary ?? "(none)"}
                      Diff present: {snapshot.Solution?.Diff is not null}
                      Confidence: {audit.Level} ({audit.ScoreSum}/12)
                      """;

        var response = await agent.RunAsync(prompt, cancellationToken: ct).ConfigureAwait(false);
        var finalReport = response.Text is { Length: > 0 } t
            ? t
            : "No final report produced.";

        var fix = snapshot.Fixability ?? new FixabilityVerdict(audit.RunId, 0, "need_more_signal", "missing");
        var ctxSummary = snapshot.Context?.Summary ?? "no context gathered";
        var hyp = snapshot.Hypothesis;
        var sol = snapshot.Solution;

        var report = new AutofixReport
        {
            FixabilityScore = fix.Score,
            FixabilityDecision = fix.Decision,
            MissingSignal = fix.MissingSignal,
            ContextSummary = ctxSummary,
            PrimaryHypothesis = hyp?.Primary ?? "no hypothesis pinned",
            AlternativeHypothesis = hyp?.Alternative,
            SolutionRepo = sol?.Repo,
            SolutionDiff = sol?.Diff,
            RegressionTest = sol?.RegressionTest,
            ConfidenceLevel = audit.Level,
            ConfidenceScoreSum = audit.ScoreSum,
            EvidenceGate = audit.EvidenceGate,
            RegressionGate = audit.RegressionGate,
            CompletenessGate = audit.CompletenessGate,
            SelfChallengeGate = audit.SelfChallengeGate,
            FinalReport = finalReport
        };

        await ledger.RecordReportAsync(audit.RunId, finalReport, ct).ConfigureAwait(false);
        await ctx.AddEventAsync(new ReportRecorded(audit.RunId, finalReport), ct).ConfigureAwait(false);

        await ctx.YieldOutputAsync(new AutofixWorkflowResult(audit.RunId, report), ct).ConfigureAwait(false);
    }

    private async ValueTask<AutofixReportAssemblyState.RunSnapshot> ResolveSnapshotAsync(
        ConfidenceAudit audit, IWorkflowContext ctx, CancellationToken ct)
    {
        var snapshot = state.Snapshot(audit.RunId);
        if (snapshot is { Fixability: not null, Context: not null, Hypothesis: not null, Solution: not null })
        {
            return snapshot;
        }

        var fixability = snapshot.Fixability ??
            await ctx.ReadStateAsync<FixabilityVerdict>(
                AutofixAssemblyKeys.Fixability, AutofixAssemblyKeys.Scope, ct).ConfigureAwait(false);
        var context = snapshot.Context ??
            await ctx.ReadStateAsync<ContextSummary>(
                AutofixAssemblyKeys.Context, AutofixAssemblyKeys.Scope, ct).ConfigureAwait(false);
        var hypothesis = snapshot.Hypothesis ??
            await ctx.ReadStateAsync<HypothesisVerdict>(
                AutofixAssemblyKeys.Hypothesis, AutofixAssemblyKeys.Scope, ct).ConfigureAwait(false);
        var solution = snapshot.Solution ??
            await ctx.ReadStateAsync<SolutionDraft>(
                AutofixAssemblyKeys.Solution, AutofixAssemblyKeys.Scope, ct).ConfigureAwait(false);
        var confidence = snapshot.Audit ?? audit;

        return new AutofixReportAssemblyState.RunSnapshot
        {
            Fixability = fixability,
            Context = context,
            Hypothesis = hypothesis,
            Solution = solution,
            Audit = confidence
        };
    }
}
