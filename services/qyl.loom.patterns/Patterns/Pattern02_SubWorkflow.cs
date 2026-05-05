
using Qyl.Loom.Patterns.Agents;
using Qyl.Loom.Patterns.Contracts;

namespace Qyl.Loom.Patterns.Patterns;

public static class Pattern02_SubWorkflow
{
    public static async Task RunAsync(IQylLoomPatternsAgentsBuilder agents, CancellationToken ct)
    {
        var rca = new RcaExecutor("patterns/02/inner/rca", agents.BuildRcaAgent());
        var solution = new SolutionExecutor("patterns/02/inner/solution", agents.BuildSolutionAgent());

        var innerWorkflow = new WorkflowBuilder(rca)
            .AddEdge(rca, solution)
            .WithOutputFrom(solution)
            .WithName("LoomPatterns/02/InnerAutofix")
            .Build();

        var autofixSubflow = innerWorkflow.BindAsExecutor("patterns/02/subflow");

        var intake = new IntakeExecutor("patterns/02/intake");
        var verdict = new VerdictExecutor("patterns/02/verdict", agents.BuildConfidenceAgent());

        var outer = new WorkflowBuilder(intake)
            .AddEdge(intake, autofixSubflow)
            .AddEdge(autofixSubflow, verdict)
            .WithOutputFrom(verdict)
            .WithName("LoomPatterns/02/OuterComposition")
            .Build();

        var signal = new IncidentSignal("S-2001", "checkout-api", "critical", "webhook retries exhausted");
        await using var run = await InProcessExecution.RunStreamingAsync(
            outer, signal, cancellationToken: ct).ConfigureAwait(false);

        await foreach (var evt in run.WatchStreamAsync(ct))
        {
            if (evt is WorkflowOutputEvent wo)
            {
                Console.WriteLine($"\n   ★ verdict   {wo.Data}");
                return;
            }
        }
    }


    private sealed class IntakeExecutor(string id) : Executor<IncidentSignal, IncidentSignal>(id)
    {
        public override ValueTask<IncidentSignal> HandleAsync(
            IncidentSignal signal, IWorkflowContext _, CancellationToken __ = default)
        {
            Console.WriteLine($"   ▶ intake    {signal.Id} ({signal.Service})");
            return ValueTask.FromResult(signal);
        }
    }

    private sealed class RcaExecutor(string id, AIAgent agent)
        : Executor<IncidentSignal, RootCauseHypothesis>(id)
    {
        public override async ValueTask<RootCauseHypothesis> HandleAsync(
            IncidentSignal signal, IWorkflowContext _, CancellationToken ct = default)
        {
            var response = await agent.RunAsync(
                $"Signal {signal.Id} on {signal.Service}: {signal.Description}",
                cancellationToken: ct).ConfigureAwait(false);
            var hypothesis = new RootCauseHypothesis(signal.Id, response.Text, 0.85);
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
                $"RCA for {rca.SignalId}: {rca.Hypothesis}",
                cancellationToken: ct).ConfigureAwait(false);
            var plan = new SolutionPlan(rca.SignalId, response.Text, [response.Text]);
            Console.WriteLine($"   ⊢ solution  {plan.Approach}");
            return plan;
        }
    }

    [YieldsOutput(typeof(ConfidenceVerdict))]
    private sealed class VerdictExecutor(string id, AIAgent agent) : Executor<SolutionPlan>(id)
    {
        public override async ValueTask HandleAsync(
            SolutionPlan plan, IWorkflowContext ctx, CancellationToken ct = default)
        {
            var response = await agent.RunAsync(
                $"Review plan for {plan.SignalId}: {plan.Approach}",
                cancellationToken: ct).ConfigureAwait(false);
            var approved = response.Text.ContainsIgnoreCase("approve");
            var verdict = new ConfidenceVerdict(plan.SignalId, approved, response.Text);
            await ctx.YieldOutputAsync(verdict, ct);
        }
    }
}
