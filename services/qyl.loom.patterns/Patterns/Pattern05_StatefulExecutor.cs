
using Qyl.Loom.Patterns.Agents;
using Qyl.Loom.Patterns.Contracts;

namespace Qyl.Loom.Patterns.Patterns;

public static class Pattern05_StatefulExecutor
{
    public static async Task RunAsync(IQylLoomPatternsAgentsBuilder agents, CancellationToken ct)
    {
        var sessionId = Guid.NewGuid().ToString("N");

        var intake = new StatefulIntake("patterns/05/intake", sessionId);
        var solution = new SolutionNode("patterns/05/solution", agents.BuildSolutionAgent());

        var workflow = new WorkflowBuilder(intake)
            .AddEdge(intake, solution)
            .WithOutputFrom(solution)
            .WithName("LoomPatterns/05/StatefulExecutor")
            .Build();

        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow,
            new IncidentSignal("S-5001", "auth-service", "critical", "JWKS cache stampede under load"),
            sessionId,
            ct);

        Console.WriteLine($"   session    {run.SessionId}");

        await foreach (var evt in run.WatchStreamAsync(ct))
        {
            switch (evt)
            {
                case StageObserved so:
                    Console.WriteLine($"   ● event    {so.Stage}  (seen={so.SeenSoFar})");
                    break;

                case WorkflowOutputEvent wo:
                    Console.WriteLine($"   ★ output   {wo.Data}");
                    return;
            }
        }
    }


    private sealed record RunState(int SignalsSeen);

    private sealed class StageObserved(string stage, int seenSoFar)
        : WorkflowEvent(new { stage, seenSoFar })
    {
        public string Stage { get; } = stage;
        public int SeenSoFar { get; } = seenSoFar;
    }


    private sealed class StatefulIntake(string id, string sessionId)
        : StatefulExecutor<RunState, IncidentSignal, RootCauseHypothesis>(
            id,
            static () => new RunState(0),
            new StatefulExecutorOptions { ScopeName = "loom/run" })
    {
        public override async ValueTask<RootCauseHypothesis> HandleAsync(
            IncidentSignal signal, IWorkflowContext ctx, CancellationToken ct = default)
        {
            RootCauseHypothesis? pending = null;

            await InvokeWithStateAsync(async (state, innerCtx, innerCt) =>
            {
                var updated = new RunState(state.SignalsSeen + 1);
                await innerCtx.AddEventAsync(new StageObserved("intake", updated.SignalsSeen), innerCt)
                    .ConfigureAwait(false);
                pending = new RootCauseHypothesis(
                    signal.Id,
                    $"Session {sessionId[..8]}: {signal.Description} — triaged #{updated.SignalsSeen}",
                    0.91);
                return updated;
            }, ctx, cancellationToken: ct).ConfigureAwait(false);

            return pending ?? throw new InvalidOperationException(
                "StatefulIntake.InvokeWithStateAsync did not run the state mutator.");
        }
    }

    [YieldsOutput(typeof(SolutionPlan))]
    private sealed class SolutionNode(string id, AIAgent agent) : Executor<RootCauseHypothesis>(id)
    {
        public override async ValueTask HandleAsync(
            RootCauseHypothesis rca, IWorkflowContext ctx, CancellationToken ct = default)
        {
            var response = await agent.RunAsync(
                $"Plan fix for: {rca.Hypothesis}", cancellationToken: ct).ConfigureAwait(false);
            await ctx.YieldOutputAsync(
                new SolutionPlan(rca.SignalId, response.Text, [response.Text]), ct);
        }
    }
}
