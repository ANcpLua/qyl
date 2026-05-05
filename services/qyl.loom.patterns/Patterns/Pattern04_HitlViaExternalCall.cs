
using Qyl.Loom.Patterns.Agents;
using Qyl.Loom.Patterns.Contracts;

namespace Qyl.Loom.Patterns.Patterns;

public static class Pattern04_HitlViaExternalCall
{
    public static async Task RunAsync(IQylLoomPatternsAgentsBuilder agents, CancellationToken ct)
    {
        var rca = new RcaExecutor("patterns/04/rca", agents.BuildRcaAgent());
        var solution = new SolutionExecutor("patterns/04/solution", agents.BuildSolutionAgent());
        var finalize = new FinalizeExecutor("patterns/04/finalize");

        var workflow = new WorkflowBuilder(rca)
            .AddEdge(rca, solution)
            .AddExternalCall<SolutionPlan, ConfidenceVerdict>(solution, "patterns/04/review")
            .ForwardMessage<ConfidenceVerdict>("patterns/04/review", [finalize])
            .WithOutputFrom(finalize)
            .WithName("LoomPatterns/04/HitlViaExternalCall")
            .Build();

        var signal = new IncidentSignal("S-4001", "order-service", "critical", "duplicate webhook deliveries");
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, signal, cancellationToken: ct);

        await foreach (var evt in run.WatchStreamAsync(ct))
        {
            switch (evt)
            {
                case RequestInfoEvent ri when ri.Request.TryGetDataAs<SolutionPlan>(out var plan):
                    Console.WriteLine($"   ? review    plan for {plan.SignalId}: \"{plan.Approach}\"");
                    Console.WriteLine("     (non-interactive demo → auto-approve)");
                    await run.SendResponseAsync(
                            ri.Request.CreateResponse(
                                new ConfidenceVerdict(plan.SignalId, true, "auto-approve for demo")))
                        .ConfigureAwait(false);
                    break;

                case WorkflowOutputEvent wo:
                    Console.WriteLine($"   ★ output   {wo.Data}");
                    return;
            }
        }
    }


    private sealed class RcaExecutor(string id, AIAgent agent)
        : Executor<IncidentSignal, RootCauseHypothesis>(id)
    {
        public override async ValueTask<RootCauseHypothesis> HandleAsync(
            IncidentSignal signal, IWorkflowContext _, CancellationToken ct = default)
        {
            var response = await agent.RunAsync(
                $"{signal.Id}: {signal.Description}", cancellationToken: ct).ConfigureAwait(false);
            var hypothesis = new RootCauseHypothesis(signal.Id, response.Text, 0.78);
            Console.WriteLine($"   ⊢ rca       {hypothesis.Hypothesis}");
            return hypothesis;
        }
    }

    private sealed class SolutionExecutor(string id, AIAgent agent)
        : Executor<RootCauseHypothesis, SolutionPlan>(id)
    {
        public override async ValueTask<SolutionPlan> HandleAsync(
            RootCauseHypothesis rca, IWorkflowContext _, CancellationToken ct = default)
        {
            var response = await agent.RunAsync(
                $"Plan: {rca.Hypothesis}", cancellationToken: ct).ConfigureAwait(false);
            var plan = new SolutionPlan(rca.SignalId, response.Text, [response.Text]);
            Console.WriteLine($"   ⊢ solution  {plan.Approach}");
            return plan;
        }
    }

    [YieldsOutput(typeof(string))]
    private sealed class FinalizeExecutor(string id) : Executor<ConfidenceVerdict>(id)
    {
        public override async ValueTask HandleAsync(
            ConfidenceVerdict verdict, IWorkflowContext ctx, CancellationToken ct = default)
        {
            var outcome = verdict.Approved ? "✓ APPROVED" : "✗ REJECTED";
            await ctx.YieldOutputAsync($"{outcome} {verdict.SignalId} — {verdict.Reason}", ct);
        }
    }
}
