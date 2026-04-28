// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// Generic transparent passthrough executor — used as the source of an
/// AddExternalCall HITL port. The dashboard sees a RequestInfoEvent carrying
/// the input and replies via run.SendResponseAsync with the same type.
internal sealed class StoppingPointGateExecutor<T>(string id, string gateName) : Executor<T, T>(id)
    where T : notnull
{
    public string GateName { get; } = gateName;

    public override async ValueTask<T> HandleAsync(T input, IWorkflowContext ctx, CancellationToken ct = default)
    {
        if (TryExtractRunId(input) is { } runId)
        {
            await ctx.AddEventAsync(new StoppingPointReached(runId, GateName), ct).ConfigureAwait(false);
        }
        return input;
    }

    private static string? TryExtractRunId(T input) =>
        input switch
        {
            HypothesisVerdict h => h.RunId,
            SolutionDraft s => s.RunId,
            ConfidenceAudit a => a.RunId,
            FixabilityVerdict f => f.RunId,
            ContextSummary c => c.RunId,
            _ => null
        };
}
