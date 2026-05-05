
using Microsoft.Agents.AI.Workflows;

namespace Qyl.Loom.Exploration.Workflow.Executors;

internal sealed class PlanSolutionExecutor(
    ExplorationStrategist strategist,
    ExplorationSessionStore sessionStore)
    : Executor<ExplorationRunState, ExplorationRunState>("exploration.plan_solution")
{
    public override async ValueTask<ExplorationRunState> HandleAsync(
        ExplorationRunState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (state.IsError || state.IsInterrupted)
        {
            return state;
        }

        await context
            .AddEventAsync(
                new ExplorationStreamEvent(ExplorationStreamUpdates.Progress(80, "Planning solution...")),
                cancellationToken)
            .ConfigureAwait(false);

        var solution = await strategist
            .PlanAsync(state.SessionId!, cancellationToken)
            .ConfigureAwait(false);

        if (solution is null)
        {
            return state;
        }

        sessionStore.SaveSolution(state.SessionId!, solution);

        var solutionJson = JsonSerializer.Serialize(
            solution,
            ExplorationJsonContext.Default.ExplorationSolution);

        await context
            .AddEventAsync(
                new ExplorationStreamEvent(ExplorationStreamUpdates.Content(solutionJson, "solution")),
                cancellationToken)
            .ConfigureAwait(false);

        return state with { Solution = solution };
    }
}
