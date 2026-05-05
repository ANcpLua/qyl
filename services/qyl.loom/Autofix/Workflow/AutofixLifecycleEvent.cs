
namespace Qyl.Loom.Autofix.Workflow;

internal abstract class AutofixLifecycleEvent(string runId, string stage, object payload)
    : WorkflowEvent(payload)
{
    public string RunId { get; } = runId;
    public string Stage { get; } = stage;
}

internal sealed class FixabilityRecorded(FixabilityVerdict verdict)
    : AutofixLifecycleEvent(verdict.RunId, "fixability", verdict)
{
    public FixabilityVerdict Verdict { get; } = verdict;
}

internal sealed class ContextRecorded(ContextSummary summary)
    : AutofixLifecycleEvent(summary.RunId, "context", summary)
{
    public ContextSummary Summary { get; } = summary;
}

internal sealed class HypothesisCandidateRecorded(HypothesisCandidate candidate)
    : AutofixLifecycleEvent(candidate.RunId, "hypothesis.candidate", candidate)
{
    public HypothesisCandidate Candidate { get; } = candidate;
}

internal sealed class HypothesisRecorded(HypothesisVerdict verdict)
    : AutofixLifecycleEvent(verdict.RunId, "hypothesis", verdict)
{
    public HypothesisVerdict Verdict { get; } = verdict;
}

internal sealed class SolutionRecorded(SolutionDraft draft)
    : AutofixLifecycleEvent(draft.RunId, "solution", draft)
{
    public SolutionDraft Draft { get; } = draft;
}

internal sealed class ConfidenceRecorded(ConfidenceAudit audit)
    : AutofixLifecycleEvent(audit.RunId, "confidence", audit)
{
    public ConfidenceAudit Audit { get; } = audit;
}

internal sealed class ReportRecorded(string runId, string finalReport)
    : AutofixLifecycleEvent(runId, "report", new { runId, finalReport })
{
    public string FinalReport { get; } = finalReport;
}
