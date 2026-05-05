
namespace Qyl.Loom.Autofix.Workflow;

internal interface IAutofixStepLedger
{
    ValueTask RecordFixabilityAsync(FixabilityVerdict verdict, CancellationToken ct);
    ValueTask RecordContextAsync(ContextSummary summary, CancellationToken ct);
    ValueTask RecordHypothesisCandidateAsync(HypothesisCandidate candidate, CancellationToken ct);
    ValueTask RecordHypothesisAsync(HypothesisVerdict verdict, CancellationToken ct);
    ValueTask RecordSolutionAsync(SolutionDraft draft, CancellationToken ct);
    ValueTask RecordConfidenceAsync(ConfidenceAudit audit, CancellationToken ct);
    ValueTask RecordReportAsync(string runId, string finalReport, CancellationToken ct);
}

internal sealed class CollectorAutofixStepLedger(CollectorClient collector) : IAutofixStepLedger
{
    public ValueTask RecordFixabilityAsync(FixabilityVerdict verdict, CancellationToken ct) =>
        InsertAsync(verdict.RunId, 1, "fixability",
            verdict.Decision is "continue" ? "completed" : "stopped",
            JsonSerializer.Serialize(new
            {
                score = verdict.Score,
                decision = verdict.Decision,
                missing_signal = verdict.MissingSignal
            }),
            ct);

    public ValueTask RecordContextAsync(ContextSummary summary, CancellationToken ct) =>
        InsertAsync(summary.RunId, 2, "context", "completed",
            JsonSerializer.Serialize(new
            {
                summary = summary.Summary,
                signals_found = summary.SignalsFound,
                signals_absent = summary.SignalsAbsent
            }),
            ct);

    public ValueTask RecordHypothesisCandidateAsync(HypothesisCandidate candidate, CancellationToken ct) =>
        InsertAsync(candidate.RunId, 3, $"hypothesis.candidate.{candidate.BranchId}", "completed",
            JsonSerializer.Serialize(new
            {
                branch = candidate.BranchId,
                primary = candidate.Primary,
                alternative = candidate.Alternative,
                self_reported_confidence = candidate.SelfReportedConfidence
            }),
            ct);

    public ValueTask RecordHypothesisAsync(HypothesisVerdict verdict, CancellationToken ct) =>
        InsertAsync(verdict.RunId, 3, "hypothesis", "completed",
            JsonSerializer.Serialize(new
            {
                primary = verdict.Primary,
                alternative = verdict.Alternative,
                judge_rationale = verdict.JudgeRationale,
                retry_iteration = verdict.RetryIteration
            }),
            ct);

    public ValueTask RecordSolutionAsync(SolutionDraft draft, CancellationToken ct) =>
        InsertAsync(draft.RunId, 4, "solution",
            draft.Diff is { Length: > 0 } ? "completed" : "skipped",
            JsonSerializer.Serialize(new
            {
                repo = draft.Repo,
                diff = draft.Diff,
                regression_test = draft.RegressionTest
            }),
            ct);

    public ValueTask RecordConfidenceAsync(ConfidenceAudit audit, CancellationToken ct) =>
        InsertAsync(audit.RunId, 5, "confidence", "completed",
            JsonSerializer.Serialize(new
            {
                level = audit.Level,
                sum = audit.ScoreSum,
                evidence = audit.EvidenceGate,
                regression = audit.RegressionGate,
                completeness = audit.CompletenessGate,
                self_challenge = audit.SelfChallengeGate,
                retry_requested = audit.RetryRequested
            }),
            ct);

    public ValueTask RecordReportAsync(string runId, string finalReport, CancellationToken ct) =>
        InsertAsync(runId, 6, "report", "completed",
            JsonSerializer.Serialize(new { text = finalReport }),
            ct);

    private async ValueTask InsertAsync(string runId, int stepNumber, string stepName, string status, string payloadJson,
        CancellationToken ct) =>
        await collector.InsertAutofixStepAsync(
            new AutofixStepRecord
            {
                StepId = Guid.NewGuid().ToString("N"),
                RunId = runId,
                StepNumber = stepNumber,
                StepName = stepName,
                Status = status,
                OutputJson = payloadJson
            },
            ct).ConfigureAwait(false);
}
