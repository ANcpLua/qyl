
using Qyl.Loom.Patterns.Agents;
using Qyl.Loom.Patterns.Contracts;

namespace Qyl.Loom.Patterns.Patterns;

public static class Pattern06_AllCombined
{
    public static async Task RunAsync(IQylLoomPatternsAgentsBuilder agents, CancellationToken ct)
    {
        var rca = new RcaStep("loom.patterns.rca", agents.BuildRcaAgent());
        var solution = new SolutionStep("loom.patterns.solution", agents.BuildSolutionAgent());

        var innerAutofix = new WorkflowBuilder(rca)
            .AddEdge(rca, solution)
            .WithOutputFrom(solution)
            .WithName("LoomPatterns/06/InnerAutofix")
            .Build();

        var autofixSubflow = innerAutofix.BindAsExecutor("loom.patterns.autofix.subflow");

        var intake = new StatefulIntake("loom.patterns.intake");
        var triage = new TriageRouter("loom.patterns.triage");
        var planBridge = new PlanBridge("loom.patterns.bridge");
        var infoAck = new InfoAcknowledge("loom.patterns.info");
        var approve = new ApprovedSink("loom.patterns.approve");
        var reject = new RejectedSink("loom.patterns.reject");

        var workflow = new WorkflowBuilder(intake)
            .AddEdge(intake, triage)
            .AddSwitch(triage, sw => sw
                .AddCase<IncidentSignal>(s => s is { Severity: "critical" or "warning" }, autofixSubflow)
                .WithDefault(infoAck))
            .AddEdge(autofixSubflow, planBridge)
            .AddExternalCall<SolutionPlan, ConfidenceVerdict>(planBridge, "loom.patterns.review")
            .ForwardMessage<ConfidenceVerdict>("loom.patterns.review", [approve], v => v.Approved)
            .ForwardMessage<ConfidenceVerdict>("loom.patterns.review", [reject], v => !v.Approved)
            .WithOutputFrom(approve)
            .WithOutputFrom(reject)
            .WithOutputFrom(infoAck)
            .WithName("LoomPatterns/06/Combined")
            .Build();

        var cm = CheckpointManager.Default;
        var checkpoints = new List<CheckpointInfo>();
        var sessionId = Guid.NewGuid().ToString("N");

        var signal = new IncidentSignal("S-6001", "checkout-api", "critical",
            "500s after 14:02 deploy — connection pool exhaustion suspected");

        Console.WriteLine($"   session    {sessionId}");
        Console.WriteLine($"   signal     {signal.Id} ({signal.Severity}) on {signal.Service}");
        Console.WriteLine($"              \"{signal.Description}\"\n");

        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow, signal, cm, sessionId, ct);

        await foreach (var evt in run.WatchStreamAsync(ct))
        {
            switch (evt)
            {
                case SuperStepCompletedEvent sse when sse.CompletionInfo?.Checkpoint is { } cp:
                    checkpoints.Add(cp);
                    Console.WriteLine($"   ⎯ checkpoint #{checkpoints.Count} (SuperStep {sse.StepNumber})");
                    break;

                case StageObserved so:
                    Console.WriteLine($"   ● event    {so.Stage}  (seen={so.SeenSoFar})");
                    break;

                case RequestInfoEvent ri when ri.Request.TryGetDataAs<SolutionPlan>(out var plan):
                    Console.WriteLine($"   ? review    plan for {plan.SignalId}: \"{plan.Approach}\"");
                    Console.WriteLine("     (non-interactive demo → approving)");
                    await run.SendResponseAsync(
                            ri.Request.CreateResponse(
                                new ConfidenceVerdict(plan.SignalId, true, "auto-approve for demo")))
                        .ConfigureAwait(false);
                    break;

                case WorkflowOutputEvent wo:
                    Console.WriteLine($"\n   ★ output   {wo.Data}");
                    Console.WriteLine($"\n   checkpoints captured : {checkpoints.Count}");
                    Console.WriteLine(
                        $"   status after output  : {await run.GetStatusAsync(ct).ConfigureAwait(false)}");
                    return;
            }
        }
    }


    private sealed class StageObserved(string stage, int seenSoFar)
        : WorkflowEvent(new { stage, seenSoFar })
    {
        public string Stage { get; } = stage;
        public int SeenSoFar { get; } = seenSoFar;
    }


    private sealed class StatefulIntake(string id)
        : StatefulExecutor<AutofixCombinedState, IncidentSignal, IncidentSignal>(
            id,
            static () => new AutofixCombinedState(0),
            new StatefulExecutorOptions { ScopeName = "loom/run" })
    {
        public override async ValueTask<IncidentSignal> HandleAsync(
            IncidentSignal signal, IWorkflowContext ctx, CancellationToken ct = default)
        {
            await InvokeWithStateAsync(async static (state, innerCtx, innerCt) =>
            {
                var updated = new AutofixCombinedState(state.SignalsSeen + 1);
                await innerCtx.AddEventAsync(new StageObserved("intake", updated.SignalsSeen), innerCt)
                    .ConfigureAwait(false);
                return updated;
            }, ctx, cancellationToken: ct).ConfigureAwait(false);

            return signal;
        }
    }


    private sealed class TriageRouter(string id) : Executor<IncidentSignal, IncidentSignal>(id)
    {
        public override ValueTask<IncidentSignal> HandleAsync(
            IncidentSignal signal, IWorkflowContext _, CancellationToken __ = default)
        {
            Console.WriteLine($"   ⊢ triage    {signal.Id} → {signal.Severity}");
            return ValueTask.FromResult(signal);
        }
    }

    private sealed class PlanBridge(string id) : Executor<SolutionPlan, SolutionPlan>(id)
    {
        public override ValueTask<SolutionPlan> HandleAsync(
            SolutionPlan plan, IWorkflowContext _, CancellationToken __ = default)
            => ValueTask.FromResult(plan);
    }

    private sealed class RcaStep(string id, AIAgent agent)
        : Executor<IncidentSignal, RootCauseHypothesis>(id)
    {
        public override async ValueTask<RootCauseHypothesis> HandleAsync(
            IncidentSignal signal, IWorkflowContext _, CancellationToken ct = default)
        {
            var response = await agent.RunAsync(
                $"{signal.Id} on {signal.Service}: {signal.Description}",
                cancellationToken: ct).ConfigureAwait(false);
            var hypothesis = new RootCauseHypothesis(signal.Id, response.Text, 0.88);
            Console.WriteLine($"   ⊢ rca       {hypothesis.Hypothesis}");
            return hypothesis;
        }
    }

    private sealed class SolutionStep(string id, AIAgent agent)
        : Executor<RootCauseHypothesis, SolutionPlan>(id)
    {
        public override async ValueTask<SolutionPlan> HandleAsync(
            RootCauseHypothesis rca, IWorkflowContext _, CancellationToken ct = default)
        {
            var response = await agent.RunAsync(
                $"Plan fix for {rca.SignalId}: {rca.Hypothesis}",
                cancellationToken: ct).ConfigureAwait(false);
            var plan = new SolutionPlan(rca.SignalId, response.Text, [response.Text]);
            Console.WriteLine($"   ⊢ solution  {plan.Approach}");
            return plan;
        }
    }

    [YieldsOutput(typeof(string))]
    private sealed class InfoAcknowledge(string id) : Executor<IncidentSignal>(id)
    {
        public override async ValueTask HandleAsync(
            IncidentSignal signal, IWorkflowContext ctx, CancellationToken ct = default) =>
            await ctx.YieldOutputAsync($"ℹ INFO {signal.Id} — logged, no action taken", ct);
    }

    [YieldsOutput(typeof(string))]
    private sealed class ApprovedSink(string id) : Executor<ConfidenceVerdict>(id)
    {
        public override async ValueTask HandleAsync(
            ConfidenceVerdict v, IWorkflowContext ctx, CancellationToken ct = default) =>
            await ctx.YieldOutputAsync($"✓ APPROVED {v.SignalId} — {v.Reason}", ct);
    }

    [YieldsOutput(typeof(string))]
    private sealed class RejectedSink(string id) : Executor<ConfidenceVerdict>(id)
    {
        public override async ValueTask HandleAsync(
            ConfidenceVerdict v, IWorkflowContext ctx, CancellationToken ct = default) =>
            await ctx.YieldOutputAsync($"✗ REJECTED {v.SignalId} — {v.Reason}", ct);
    }
}
