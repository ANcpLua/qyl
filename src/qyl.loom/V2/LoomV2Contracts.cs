using Qyl.Instrumentation.Instrumentation.Loom;

namespace Qyl.Loom.V2;

[LoomContract("loom.v2.run.context")]
public sealed record LoomV2RunContext(
    string RunId,
    string IssueId,
    string? BaselineWindow = null,
    string? ComparisonWindow = null,
    int Attempt = 0,
    int MaxAttempts = 3);

[LoomContract("loom.v2.run.state")]
public sealed record LoomV2RunState(
    LoomV2RunContext Context,
    LoomV2RunStatus Status = LoomV2RunStatus.Pending,
    LoomV2RegressionAnalysis? Detection = null,
    LoomV2FixPlan? Plan = null,
    LoomV2PatchProposal? Patch = null,
    LoomV2VerificationResult? Verification = null,
    LoomV2InvestigationReport? Report = null,
    LoomV2ClosureDecision? Closure = null)
{
    public static LoomV2RunState Start(LoomV2RunContext context) =>
        new(context, LoomV2RunStatus.Pending);
}

public enum LoomV2RunStatus
{
    Pending,
    Running,
    WaitingApproval,
    Completed,
    Failed
}

[LoomContract("loom.v2.analyze_regression.input")]
public sealed record LoomV2AnalyzeRegressionInput(
    string BaselineWindow,
    string ComparisonWindow,
    string SignalType,
    string? ExpectedBehavior = null);

[LoomContract("loom.v2.regression_analysis")]
public sealed record LoomV2RegressionAnalysis(
    string BaselineWindow,
    string ComparisonWindow,
    string SignalType,
    bool RegressionDetected,
    double DeltaPercent,
    string Summary,
    IReadOnlyList<string> SuspectedCauses);

[LoomContract("loom.v2.fix_plan")]
public sealed record LoomV2FixPlan(
    string IssueId,
    string Summary,
    IReadOnlyList<string> Steps,
    double EstimatedRisk);

[LoomContract("loom.v2.patch_proposal")]
public sealed record LoomV2PatchProposal(
    string IssueId,
    string Summary,
    IReadOnlyList<string> ChangedFiles,
    string UnifiedDiff,
    bool RequiresApproval);

[LoomContract("loom.v2.verification_result")]
public sealed record LoomV2VerificationResult(
    string IssueId,
    bool Passed,
    string Summary,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Evidence);

[LoomContract("loom.v2.investigation_report")]
public sealed record LoomV2InvestigationReport(
    string IssueId,
    string Summary,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> NextSteps);

[LoomContract("loom.v2.closure_decision")]
public sealed record LoomV2ClosureDecision(
    string IssueId,
    bool CloseIssue,
    string Reason);

[LoomContract("loom.v2.detect.command")]
public sealed record LoomV2DetectCommand(
    LoomV2RunContext Context,
    LoomV2AnalyzeRegressionInput RegressionInput);

[LoomContract("loom.v2.plan.command")]
public sealed record LoomV2PlanCommand(LoomV2RunState State);

[LoomContract("loom.v2.fix.command")]
public sealed record LoomV2FixCommand(LoomV2RunState State);

[LoomContract("loom.v2.verify.command")]
public sealed record LoomV2VerifyCommand(LoomV2RunState State);

[LoomContract("loom.v2.report.command")]
public sealed record LoomV2ReportCommand(LoomV2RunState State);

[LoomContract("loom.v2.close.command")]
public sealed record LoomV2CloseCommand(LoomV2RunState State);
