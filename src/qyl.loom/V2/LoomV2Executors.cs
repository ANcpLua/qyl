using Qyl.Instrumentation.Instrumentation.Loom;

namespace Qyl.Loom.V2;

internal interface ILoomV2Executor<in TInput, TOutput>
{
    ValueTask<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
}

[LoomStep(
    "loom.v2.detect",
    Phase = LoomPhase.Detect,
    Description = "Detect regression evidence and seed the run state.")]
internal sealed partial class LoomV2DetectExecutor : ILoomV2Executor<LoomV2DetectCommand, LoomV2RunState>
{
    public ValueTask<LoomV2RunState> ExecuteAsync(
        LoomV2DetectCommand input,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var detection = LoomV2Tools.AnalyzeRegression(input.RegressionInput);
        var state = new LoomV2RunState(
            input.Context,
            LoomV2RunStatus.Running,
            Detection: detection);

        return ValueTask.FromResult(state);
    }
}

[LoomStep(
    "loom.v2.plan",
    Phase = LoomPhase.Plan,
    Description = "Transform detected regression evidence into a bounded fix plan.")]
internal sealed partial class LoomV2PlanExecutor : ILoomV2Executor<LoomV2PlanCommand, LoomV2RunState>
{
    public ValueTask<LoomV2RunState> ExecuteAsync(
        LoomV2PlanCommand input,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var state = input.State;
        var detection = state.Detection;
        var plan = new LoomV2FixPlan(
            state.Context.IssueId,
            detection is null
                ? "Plan the next diagnostic actions."
                : $"Plan the fix for: {detection.Summary}",
            [
                "inspect offending subsystem",
                "draft minimal code change",
                "prepare validation checklist"
            ],
            EstimatedRisk: detection?.RegressionDetected == true ? 0.42d : 0.10d);

        return ValueTask.FromResult(state with
        {
            Plan = plan,
            Status = LoomV2RunStatus.Running
        });
    }
}

[LoomStep(
    "loom.v2.fix",
    Phase = LoomPhase.Fix,
    Description = "Turn the plan into a patch proposal without mutating shared state.")]
internal sealed partial class LoomV2FixExecutor : ILoomV2Executor<LoomV2FixCommand, LoomV2RunState>
{
    public ValueTask<LoomV2RunState> ExecuteAsync(
        LoomV2FixCommand input,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var state = input.State;
        var patch = new LoomV2PatchProposal(
            state.Context.IssueId,
            state.Plan?.Summary ?? "Draft a minimal patch proposal.",
            [
                "src/qyl.loom/V2/LoomV2Contracts.cs",
                "src/qyl.loom/V2/LoomV2Workflow.cs"
            ],
            UnifiedDiff: "--- a/... \n+++ b/...",
            RequiresApproval: true);

        return ValueTask.FromResult(state with
        {
            Patch = patch,
            Status = LoomV2RunStatus.WaitingApproval
        });
    }
}

[LoomStep(
    "loom.v2.verify",
    Phase = LoomPhase.Verify,
    Description = "Verify the proposed patch in isolation before any closure decision.")]
internal sealed partial class LoomV2VerifyExecutor : ILoomV2Executor<LoomV2VerifyCommand, LoomV2RunState>
{
    public ValueTask<LoomV2RunState> ExecuteAsync(
        LoomV2VerifyCommand input,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var state = input.State;
        var verification = new LoomV2VerificationResult(
            state.Context.IssueId,
            Passed: true,
            Summary: "Verification passed in the V2 skeleton.",
            [
                "unit checks",
                "behavioral checks",
                "diff inspection"
            ],
            [
                "synthetic verification evidence"
            ]);

        return ValueTask.FromResult(state with
        {
            Verification = verification,
            Status = LoomV2RunStatus.Running
        });
    }
}

[LoomStep(
    "loom.v2.report",
    Phase = LoomPhase.Report,
    Description = "Summarize the investigation and produce operator-ready findings.")]
internal sealed partial class LoomV2ReportExecutor : ILoomV2Executor<LoomV2ReportCommand, LoomV2RunState>
{
    public ValueTask<LoomV2RunState> ExecuteAsync(
        LoomV2ReportCommand input,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var state = input.State;
        var report = new LoomV2InvestigationReport(
            state.Context.IssueId,
            Summary: state.Verification?.Summary ?? "Produce the final investigation report.",
            [
                state.Detection?.Summary ?? "No detection summary available.",
                state.Plan?.Summary ?? "No plan summary available.",
                state.Verification?.Summary ?? "No verification summary available."
            ],
            [
                "publish findings",
                "close if approval is granted"
            ]);

        return ValueTask.FromResult(state with
        {
            Report = report,
            Status = LoomV2RunStatus.Running
        });
    }
}

[LoomStep(
    "loom.v2.close",
    Phase = LoomPhase.Close,
    Description = "Resolve the run with an explicit closure decision.")]
internal sealed partial class LoomV2CloseExecutor : ILoomV2Executor<LoomV2CloseCommand, LoomV2RunState>
{
    public ValueTask<LoomV2RunState> ExecuteAsync(
        LoomV2CloseCommand input,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var state = input.State;
        var close = new LoomV2ClosureDecision(
            state.Context.IssueId,
            CloseIssue: state.Verification?.Passed == true,
            Reason: state.Verification?.Passed == true
                ? "Verification passed and the run is ready to close."
                : "Verification did not pass.");

        return ValueTask.FromResult(state with
        {
            Closure = close,
            Status = close.CloseIssue ? LoomV2RunStatus.Completed : LoomV2RunStatus.Failed
        });
    }
}
