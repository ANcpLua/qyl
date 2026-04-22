// Copyright (c) 2025-2026 ancplua

using Qyl.Loom.Patterns.Agents;
using Qyl.Loom.Patterns.Contracts;

namespace Qyl.Loom.Patterns;

/// <summary>
///     Pattern 05 — <c>StatefulExecutor&lt;TState, TInput, TOutput&gt;</c> +
///     <c>InvokeWithStateAsync</c>. The intake executor owns mutable
///     <see cref="RunState"/> scoped to <c>"loom/run"</c>, read &amp; queued around
///     every invocation by the base class. Also demonstrates
///     <c>ctx.AddEventAsync</c> for lifecycle events as <see cref="WorkflowEvent"/>
///     subclasses — consumers pattern-match on type in <c>WatchStreamAsync</c>.
/// </summary>
public static class Pattern05_StatefulExecutor
{
    /// <summary>Runs the stateful-executor demonstration end-to-end.</summary>
    public static async Task RunAsync(IQylLoomPatternsAgentsBuilder agents, CancellationToken ct)
    {
        var sessionId = Guid.NewGuid().ToString("N");

        var intake   = new StatefulIntake("patterns/05/intake", sessionId);
        var solution = new SolutionNode("patterns/05/solution", agents.BuildSolutionAgent());

        Workflow workflow = new WorkflowBuilder(intake)
            .AddEdge(intake, solution)
            .WithOutputFrom(solution)
            .WithName("LoomPatterns/05/StatefulExecutor")
            .Build();

        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow,
            new IncidentSignal("S-5001", "auth-service", "critical", "JWKS cache stampede under load"),
            sessionId: sessionId,
            cancellationToken: ct);

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

    // ── State + lifecycle event ──────────────────────────────────────────────

    /// <summary>Per-run state — how many signals the intake has triaged.</summary>
    private sealed record RunState(int SignalsSeen);

    /// <summary>
    ///     Lifecycle event — a <see cref="WorkflowEvent"/> subclass pattern-matched in
    ///     <c>WatchStreamAsync</c>. Carries the running count so consumers can correlate.
    /// </summary>
    private sealed class StageObserved(string stage, int seenSoFar)
        : WorkflowEvent(data: new { stage, seenSoFar })
    {
        public string Stage { get; } = stage;
        public int SeenSoFar { get; } = seenSoFar;
    }

    // ── Executors ────────────────────────────────────────────────────────────

    /// <summary>
    ///     Stateful intake — increments the per-run counter via <c>InvokeWithStateAsync</c>
    ///     and emits a <see cref="StageObserved"/> lifecycle event before forwarding the
    ///     signal downstream as a root-cause hypothesis.
    /// </summary>
    private sealed class StatefulIntake(string id, string sessionId)
        : StatefulExecutor<RunState, IncidentSignal, RootCauseHypothesis>(
            id,
            initialStateFactory: static () => new RunState(SignalsSeen: 0),
            options: new StatefulExecutorOptions { ScopeName = "loom/run" })
    {
        private RootCauseHypothesis _pending = null!;

        public override async ValueTask<RootCauseHypothesis> HandleAsync(
            IncidentSignal signal, IWorkflowContext ctx, CancellationToken ct = default)
        {
            await InvokeWithStateAsync(async (state, innerCtx, innerCt) =>
            {
                var updated = state with { SignalsSeen = state.SignalsSeen + 1 };
                await innerCtx.AddEventAsync(new StageObserved("intake", updated.SignalsSeen), innerCt)
                    .ConfigureAwait(false);
                _pending = new RootCauseHypothesis(
                    signal.Id,
                    $"Session {sessionId[..8]}: {signal.Description} — triaged #{updated.SignalsSeen}",
                    Confidence: 0.91);
                return updated;
            }, ctx, cancellationToken: ct).ConfigureAwait(false);

            return _pending;
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
                new SolutionPlan(rca.SignalId, response.Text, Steps: [response.Text]), ct);
        }
    }
}
