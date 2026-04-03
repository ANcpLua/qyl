namespace Qyl.Loom.V2;

[LoomWorkflow(
    "loom.v2.full_cycle",
    Description = "Fresh workflow per run: detect -> plan -> fix -> verify -> report -> close.")]
public static class LoomV2WorkflowFactory
{
    public static LoomV2WorkflowRun Create(LoomV2RunContext context) => new(context);
}

public sealed class LoomV2WorkflowRun
{
    private readonly LoomV2DetectExecutor _detect = new();
    private readonly LoomV2PlanExecutor _plan = new();
    private readonly LoomV2FixExecutor _fix = new();
    private readonly LoomV2VerifyExecutor _verify = new();
    private readonly LoomV2ReportExecutor _report = new();
    private readonly LoomV2CloseExecutor _close = new();

    public LoomV2WorkflowRun(LoomV2RunContext context) => Context = context;

    public LoomV2RunContext Context { get; }

    public async ValueTask<LoomV2RunState> RunAsync(CancellationToken cancellationToken = default)
    {
        var state = LoomV2RunState.Start(Context) with
        {
            Status = LoomV2RunStatus.Running
        };

        state = await _detect.ExecuteAsync(
            new LoomV2DetectCommand(Context, CreateRegressionInput()),
            cancellationToken).ConfigureAwait(false);

        state = await _plan.ExecuteAsync(new LoomV2PlanCommand(state), cancellationToken).ConfigureAwait(false);
        state = await _fix.ExecuteAsync(new LoomV2FixCommand(state), cancellationToken).ConfigureAwait(false);
        state = await _verify.ExecuteAsync(new LoomV2VerifyCommand(state), cancellationToken).ConfigureAwait(false);
        state = await _report.ExecuteAsync(new LoomV2ReportCommand(state), cancellationToken).ConfigureAwait(false);
        state = await _close.ExecuteAsync(new LoomV2CloseCommand(state), cancellationToken).ConfigureAwait(false);

        return state;
    }

    private LoomV2AnalyzeRegressionInput CreateRegressionInput()
    {
        var context = Context;

        return new LoomV2AnalyzeRegressionInput(
            context.BaselineWindow ?? "baseline",
            context.ComparisonWindow ?? "comparison",
            SignalType: "errors",
            ExpectedBehavior: "stable behavior");
    }
}

internal static class LoomV2RunStateExtensions
{
}
