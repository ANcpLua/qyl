using Qyl.Instrumentation.Instrumentation.Loom;

namespace Qyl.Loom.CompilerDemo;

[LoomContract("loom_demo_analyze_regression_input")]
public sealed partial record LoomDemoAnalyzeRegressionInput(
    string BaselineWindow,
    string ComparisonWindow,
    SignalType SignalType,
    string? ExpectedBehavior = null);

[LoomContract("loom_demo_regression_analysis")]
public sealed partial record LoomDemoRegressionAnalysis(
    string BaselineWindow,
    string ComparisonWindow,
    SignalType SignalType,
    bool RegressionDetected,
    double DeltaPercent,
    string Summary,
    IReadOnlyList<string> SuspectedCauses);

[LoomContract("loom_demo_root_cause_report")]
public sealed partial record LoomDemoRootCauseReport(
    string Summary,
    IReadOnlyList<string> Causes,
    IReadOnlyList<string> Evidence);

[LoomContract("loom_demo_fix_plan")]
public sealed partial record LoomDemoFixPlan(
    string Summary,
    IReadOnlyList<string> Steps,
    double EstimatedRisk);

[LoomContract("loom_demo_patch_proposal")]
public sealed partial record LoomDemoPatchProposal(
    string Summary,
    string Diff,
    IReadOnlyList<string> Files);

[LoomContract("loom_demo_verification_result")]
public sealed partial record LoomDemoVerificationResult(
    bool Passed,
    string Summary,
    double Confidence,
    IReadOnlyList<string> Checks);

[LoomContract("loom_demo_investigation_report")]
public sealed partial record LoomDemoInvestigationReport(
    string Summary,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Actions);

[LoomContract("loom_demo_closure_decision")]
public sealed partial record LoomDemoClosureDecision(
    bool Closed,
    string Reason);

[LoomContract("loom_demo_run_state")]
public sealed partial record LoomDemoRunState(
    string RunId,
    string IssueId,
    RunStatus Status,
    LoomDemoRegressionAnalysis? Detection = null,
    LoomDemoRootCauseReport? RootCause = null,
    LoomDemoFixPlan? Plan = null,
    LoomDemoPatchProposal? Patch = null,
    LoomDemoVerificationResult? Verification = null,
    LoomDemoInvestigationReport? Report = null,
    LoomDemoClosureDecision? Closure = null,
    int Attempt = 0,
    int MaxAttempts = 3);
