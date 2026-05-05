
using Microsoft.Agents.AI.Workflows;

namespace Qyl.Loom.Exploration.Workflow.Executors;

internal sealed class DiagnoseExecutor(
    ExplorationDiagnostician diagnostician,
    ExplorationSessionStore sessionStore)
    : Executor<ExplorationRunState, ExplorationRunState>("exploration.diagnose")
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
                new ExplorationStreamEvent(
                    ExplorationStreamUpdates.Progress(20, "Figuring out the root cause...")),
                cancellationToken)
            .ConfigureAwait(false);

        var diagnosis = await diagnostician
            .DiagnoseAsync(
                state.Context!,
                update =>
                    context.AddEventAsync(new ExplorationStreamEvent(update), cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);

        if (diagnosis.IsInterrupted)
        {
            await context
                .AddEventAsync(
                    new ExplorationStreamEvent(
                        ExplorationStreamUpdates.Progress(100, "Exploration interrupted.")),
                    cancellationToken)
                .ConfigureAwait(false);
            return state with { IsInterrupted = true };
        }

        sessionStore.SaveDiagnosis(state.SessionId!, diagnosis.Monologue, diagnosis.RootCause);

        await context
            .AddEventAsync(
                new ExplorationStreamEvent(ExplorationStreamUpdates.Progress(60, "Synthesizing root cause...")),
                cancellationToken)
            .ConfigureAwait(false);

        if (diagnosis.RootCause is not null)
        {
            var rootCauseJson = JsonSerializer.Serialize(
                diagnosis.RootCause,
                ExplorationJsonContext.Default.ExplorationRootCause);

            await context
                .AddEventAsync(
                    new ExplorationStreamEvent(ExplorationStreamUpdates.Content(rootCauseJson, "root_cause")),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return state with { RootCause = diagnosis.RootCause };
    }
}
