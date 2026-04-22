// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI.Workflows;

namespace Qyl.Loom.Exploration.Workflow.Executors;

/// <summary>
///     Step 2: runs the <see cref="ExplorationDiagnostician" /> with a per-chunk callback that forwards
///     streaming LLM output to the workflow stream as <see cref="ExplorationStreamEvent" />s. This replaces
///     the previous buffered behavior (updates collected into a list and flushed after the LLM finished).
/// </summary>
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
                onChunk: update =>
                    context.AddEventAsync(new ExplorationStreamEvent(update), cancellationToken),
                ct: cancellationToken)
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
