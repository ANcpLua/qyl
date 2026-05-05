
using Microsoft.Agents.AI.Workflows;

namespace Qyl.Loom.Exploration.Workflow.Executors;

[YieldsOutput(typeof(ExplorationRunState))]
internal sealed partial class FinalizeExplorationExecutor(ILogger<FinalizeExplorationExecutor> logger)
    : Executor<ExplorationRunState>("exploration.finalize")
{
    public override async ValueTask HandleAsync(
        ExplorationRunState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (!state.IsError)
        {
            if (state.IsInterrupted)
            {
                await context
                    .AddEventAsync(
                        new ExplorationStreamEvent(ExplorationStreamUpdates.Completed()),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await context
                    .AddEventAsync(
                        new ExplorationStreamEvent(
                            ExplorationStreamUpdates.Progress(100, "Formatting for human consumption...")),
                        cancellationToken)
                    .ConfigureAwait(false);
                await context
                    .AddEventAsync(
                        new ExplorationStreamEvent(ExplorationStreamUpdates.Completed()),
                        cancellationToken)
                    .ConfigureAwait(false);

                LogExplorationCompleted(
                    state.IssueId,
                    state.RootCause?.Steps.Length ?? 0,
                    state.Solution?.Steps.Length ?? 0);
            }
        }

        await context.YieldOutputAsync(state, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Exploration completed for issue {IssueId}: {RcaSteps} RCA steps, {SolutionSteps} solution steps")]
    private partial void LogExplorationCompleted(string issueId, int rcaSteps, int solutionSteps);
}
