using Microsoft.Agents.AI.Workflows;
using Qyl.Instrumentation.Instrumentation.Loom;

namespace Qyl.Loom.CompilerDemo;

[LoomStep("loom.demo.detect", Phase = LoomPhase.Detect, Description = "Detect regression and localize evidence.")]
internal sealed partial class LoomDemoDetectExecutor() : Executor("loom.demo.detect")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol)
    {
        protocol.RouteBuilder.AddHandler<LoomDemoAnalyzeRegressionInput, LoomDemoRegressionAnalysis>(HandleAsync);
        return protocol;
    }

    private ValueTask<LoomDemoRegressionAnalysis> HandleAsync(
        LoomDemoAnalyzeRegressionInput input,
        IWorkflowContext context)
        => ValueTask.FromResult(LoomDemoTools.AnalyzeRegression(input));
}

[LoomStep("loom.demo.plan", Phase = LoomPhase.Plan, Description = "Produce RCA and a bounded fix plan.")]
internal sealed partial class LoomDemoPlanExecutor() : Executor("loom.demo.plan")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol)
    {
        protocol.RouteBuilder.AddHandler<LoomDemoRegressionAnalysis, LoomDemoFixPlan>(HandleAsync);
        return protocol;
    }

    private ValueTask<LoomDemoFixPlan> HandleAsync(
        LoomDemoRegressionAnalysis analysis,
        IWorkflowContext context)
        => ValueTask.FromResult(
            new LoomDemoFixPlan(
                "Plan from structured analysis.",
                ["inspect deployment", "remove risky path"],
                0.15));
}

[LoomStep("loom.demo.fix", Phase = LoomPhase.Fix, Description = "Generate a candidate patch proposal.")]
internal sealed partial class LoomDemoFixExecutor() : Executor("loom.demo.fix")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol)
    {
        protocol.RouteBuilder.AddHandler<LoomDemoFixPlan, LoomDemoPatchProposal>(HandleAsync);
        return protocol;
    }

    private ValueTask<LoomDemoPatchProposal> HandleAsync(
        LoomDemoFixPlan plan,
        IWorkflowContext context)
        => ValueTask.FromResult(
            new LoomDemoPatchProposal(
                plan.Summary,
                "diff --git a/...",
                ["Service.cs", "Cache.cs"]));
}

[LoomStep("loom.demo.verify", Phase = LoomPhase.Verify, Description = "Verify patch safety and effect.")]
internal sealed partial class LoomDemoVerifyExecutor() : Executor("loom.demo.verify")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol)
    {
        protocol.RouteBuilder.AddHandler<LoomDemoPatchProposal, LoomDemoVerificationResult>(HandleAsync);
        return protocol;
    }

    private ValueTask<LoomDemoVerificationResult> HandleAsync(
        LoomDemoPatchProposal patch,
        IWorkflowContext context)
        => ValueTask.FromResult(LoomDemoTools.VerifyFix(patch));
}

[LoomStep("loom.demo.report", Phase = LoomPhase.Report, Description = "Project artifacts into an operator-grade report.")]
internal sealed partial class LoomDemoReportExecutor() : Executor("loom.demo.report")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol)
    {
        protocol.RouteBuilder.AddHandler<LoomDemoVerificationResult, LoomDemoInvestigationReport>(HandleAsync);
        return protocol;
    }

    private ValueTask<LoomDemoInvestigationReport> HandleAsync(
        LoomDemoVerificationResult verification,
        IWorkflowContext context)
        => ValueTask.FromResult(
            new LoomDemoInvestigationReport(
                "Regression resolved.",
                [verification.Summary],
                ["publish report", "prepare closure"]));
}

[LoomStep("loom.demo.close", Phase = LoomPhase.Close, Description = "Close issue via explicit approval boundary.")]
internal sealed partial class LoomDemoCloseExecutor() : Executor("loom.demo.close")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol)
    {
        protocol.RouteBuilder.AddHandler<LoomDemoClosureDecision>(HandleAsync);
        return protocol;
    }

    private async ValueTask HandleAsync(
        LoomDemoClosureDecision decision,
        IWorkflowContext context)
        => await context.YieldOutputAsync(decision);
}

[LoomWorkflow(
    "loom.demo.full_cycle",
    typeof(LoomDemoRunState),
    "loom.demo.detect",
    "loom.demo.plan",
    "loom.demo.fix",
    "loom.demo.verify",
    "loom.demo.report",
    "loom.demo.close",
    Description = "Detect -> plan -> fix -> verify -> report -> close")]
public static partial class LoomDemoWorkflowFactory
{
    public static Workflow Build()
    {
        var approvalPort =
            RequestPort.Create<LoomDemoInvestigationReport, LoomDemoClosureDecision>(
                "loom.demo.close.approval");

        var detect = new LoomDemoDetectExecutor();
        var plan = new LoomDemoPlanExecutor();
        var fix = new LoomDemoFixExecutor();
        var verify = new LoomDemoVerifyExecutor();
        var report = new LoomDemoReportExecutor();
        var close = new LoomDemoCloseExecutor();

        return new WorkflowBuilder(detect)
            .AddEdge(detect, plan)
            .AddEdge(plan, fix)
            .AddEdge(fix, verify)
            .AddEdge(verify, report)
            .AddEdge(report, approvalPort)
            .AddEdge(approvalPort, close)
            .WithOutputFrom(close)
            .Build();
    }
}
