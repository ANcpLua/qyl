// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI.Workflows;

namespace Qyl.Loom.Autofix.Workflow;

/// <summary>
///     Base class for mid-pipeline autofix executors. Handles step-ledger bookkeeping
///     and honors <see cref="AutofixRunState.IsEarlyStop" /> / <see cref="AutofixRunState.IsFatalError" /> by
///     passing state through untouched. Concrete executors implement <see cref="DoWorkAsync" /> with their
///     one pipeline step of work.
/// </summary>
/// <remarks>
///     The narrow <c>catch</c> in <see cref="HandleAsync" /> intentionally only recovers from two classes of
///     failure that are data-driven and per-step: transport failures hitting the collector REST API
///     or an LLM provider (<see cref="HttpRequestException" />), and malformed LLM output that cannot be parsed
///     (<see cref="JsonException" />). These are marked as a fatal error on the state and propagated forward —
///     the terminal policy gate transitions the run. Every other exception type escapes so the scheduler
///     records it at the run level and the step row is left in <c>running</c> as a visible bug signal.
/// </remarks>
internal abstract class AutofixPipelineExecutor(
    string executorId,
    int stepNumber,
    string stepName,
    CollectorClient collector)
    : Executor<AutofixRunState, AutofixRunState>(executorId)
{
    protected CollectorClient Collector { get; } = collector;

    public sealed override async ValueTask<AutofixRunState> HandleAsync(
        AutofixRunState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (state.IsEarlyStop || state.IsFatalError)
        {
            return state;
        }

        var stepId = Guid.NewGuid().ToString("N");
        var step = new AutofixStepRecord
        {
            StepId = stepId,
            RunId = state.RunId,
            StepNumber = stepNumber,
            StepName = stepName,
            Status = "running",
        };

        await Collector.InsertAutofixStepAsync(step, cancellationToken).ConfigureAwait(false);

        try
        {
            var (next, outputJson) = await DoWorkAsync(state, cancellationToken).ConfigureAwait(false);
            await Collector
                .UpdateAutofixStepAsync(state.RunId, stepId, "completed", outputJson, ct: cancellationToken)
                .ConfigureAwait(false);
            return next;
        }
        catch (HttpRequestException ex)
        {
            return await MarkStepFailedAsync(state, stepId, ex, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            return await MarkStepFailedAsync(state, stepId, ex, cancellationToken).ConfigureAwait(false);
        }
    }

    protected abstract ValueTask<(AutofixRunState State, string OutputJson)> DoWorkAsync(
        AutofixRunState state,
        CancellationToken cancellationToken);

    private async ValueTask<AutofixRunState> MarkStepFailedAsync(
        AutofixRunState state, string stepId, Exception ex, CancellationToken cancellationToken)
    {
        await Collector
            .UpdateAutofixStepAsync(state.RunId, stepId, "failed", errorMessage: ex.Message, ct: cancellationToken)
            .ConfigureAwait(false);

        return state with
        {
            IsFatalError = true,
            FatalErrorMessage = $"{stepName}: {ex.GetType().Name}: {ex.Message}",
        };
    }
}
