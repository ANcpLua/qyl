namespace Qyl.Loom.CompilerDemo;

using System.ComponentModel;
using Instrumentation.Instrumentation.Loom;

public static partial class LoomDemoTools
{
    [LoomTool("analyze_regression",
        Description =
            "Use this only when comparing baseline vs current system behavior to detect meaningful regressions.",
        Phase = LoomPhase.Detect,
        UseOnlyWhen = "comparing a stable baseline window to a suspect comparison window",
        DoNotUseWhen = "raw logging, metric dumping, or generic telemetry export")]
    [RequiresCapability("qyl.regression.analyze")]
    [ToolSideEffect(ToolSideEffect.None)]
    [EmitsStructuredOutput(typeof(LoomDemoRegressionAnalysis))]
    public static LoomDemoRegressionAnalysis AnalyzeRegression(
        [Description("Time range representing stable system behavior")]
        LoomDemoAnalyzeRegressionInput input) =>
        new(
            input.BaselineWindow,
            input.ComparisonWindow,
            input.SignalType,
            true,
            37.5,
            "Latency and error rate regressed after a deployment boundary.",
            ["suspect deployment", "cache invalidation", "query plan drift"]);

    [LoomTool("propose_fix",
        Description = "Generate a bounded remediation plan from structured RCA evidence.",
        Phase = LoomPhase.Plan)]
    [RequiresCapability("qyl.fix.plan")]
    [ToolSideEffect(ToolSideEffect.None)]
    [EmitsStructuredOutput(typeof(LoomDemoFixPlan))]
    public static LoomDemoFixPlan ProposeFix(LoomDemoRootCauseReport report) =>
        new(
            "Rollback the suspect optimization and add a bounded guard.",
            ["disable risky path", "restore previous plan", "add regression check"],
            0.21);

    [LoomTool("verify_fix",
        Description = "Verify that the proposed fix removes the regression and does not create a new one.",
        Phase = LoomPhase.Verify)]
    [RequiresCapability("qyl.fix.verify")]
    [ToolSideEffect(ToolSideEffect.ReadsExternalState)]
    [EmitsStructuredOutput(typeof(LoomDemoVerificationResult))]
    public static LoomDemoVerificationResult VerifyFix(
        LoomDemoPatchProposal proposal,
        CancellationToken cancellationToken = default) =>
        new(
            true,
            "Replay and smoke checks passed.",
            0.93,
            ["replay", "latency", "errors"]);

    [LoomTool("close_issue",
        Description = "Close the issue after successful verification and report publication.",
        Phase = LoomPhase.Close)]
    [RequiresCapability("qyl.issue.close")]
    [RequiresApproval]
    [ToolSideEffect(ToolSideEffect.ClosesIssue)]
    [EmitsStructuredOutput(typeof(LoomDemoClosureDecision))]
    public static LoomDemoClosureDecision CloseIssue(string issueId, string reason) =>
        new(true, $"Closed {issueId}: {reason}");
}
