// Copyright (c) 2025-2026 ancplua

using Qyl.Loom.Patterns.Agents;
using Qyl.Loom.Patterns.Contracts;

namespace Qyl.Loom.Patterns;

/// <summary>
///     Pattern 06 — autofix-shaped end-to-end demonstration that combines every MAF-1.1
///     composition primitive in one graph.
///     <list type="bullet">
///       <item><c>StreamingRun.SessionId</c> — caller-minted, round-tripped through <c>RunStreamingAsync</c>.</item>
///       <item><c>AddSwitch</c> + <c>AddCase&lt;T&gt;</c> + <c>WithDefault</c> — severity triage.</item>
///       <item><c>Workflow.BindAsExecutor</c> — inner <c>rca → solution</c> sub-workflow as a single node.</item>
///       <item><c>StatefulExecutor&lt;TState, TInput, TOutput&gt;</c> + <c>InvokeWithStateAsync</c> — intake counter.</item>
///       <item><c>ctx.AddEventAsync</c> + <see cref="StageObserved"/> — lifecycle events.</item>
///       <item><c>AddExternalCall&lt;TReq, TResp&gt;</c> — one-line HITL port.</item>
///       <item><c>ForwardMessage&lt;TResp&gt;</c> with a predicate — typed fan-out to approve / reject sinks.</item>
///       <item><c>CheckpointManager</c> + <c>SuperStepCompletedEvent.CompletionInfo.Checkpoint</c> — auto-snapshotting.</item>
///       <item><c>StreamingRun.GetStatusAsync</c> — post-run diagnostics.</item>
///     </list>
/// </summary>
public static class Pattern06_AllCombined
{
    /// <summary>Runs the combined autofix demonstration end-to-end.</summary>
    public static async Task RunAsync(IQylLoomPatternsAgentsBuilder agents, CancellationToken ct)
    {
        // ── Inner autofix sub-workflow: rca → solution ───────────────────────
        var rca      = new RcaStep("loom.patterns.rca", agents.BuildRcaAgent());
        var solution = new SolutionStep("loom.patterns.solution", agents.BuildSolutionAgent());

        Workflow innerAutofix = new WorkflowBuilder(rca)
            .AddEdge(rca, solution)
            .WithOutputFrom(solution)
            .WithName("LoomPatterns/06/InnerAutofix")
            .Build();

        ExecutorBinding autofixSubflow = innerAutofix.BindAsExecutor("loom.patterns.autofix.subflow");

        // ── Outer graph ──────────────────────────────────────────────────────
        var intake      = new StatefulIntake("loom.patterns.intake");
        var triage      = new TriageRouter("loom.patterns.triage");
        var planBridge  = new PlanBridge("loom.patterns.bridge");
        var infoAck     = new InfoAcknowledge("loom.patterns.info");
        var approve     = new ApprovedSink("loom.patterns.approve");
        var reject      = new RejectedSink("loom.patterns.reject");

        Workflow workflow = new WorkflowBuilder(intake)
            .AddEdge(intake, triage)
            // Severity fan-out — critical/warning enter the autofix subflow, info short-circuits.
            .AddSwitch(triage, sw => sw
                .AddCase<IncidentSignal>(s => s is { Severity: "critical" or "warning" }, autofixSubflow)
                .WithDefault(infoAck))
            // Subflow's SolutionPlan output flows into the pass-through bridge. The bridge is a
            // regular executor, so the port's bidirectional back-edge (port → bridge) doesn't
            // interfere with the subflow's own protocol.
            .AddEdge(autofixSubflow, planBridge)
            // One-line HITL — AddExternalCall creates the port + both edges.
            .AddExternalCall<SolutionPlan, ConfidenceVerdict>(planBridge, portId: "loom.patterns.review")
            // Typed forward: verdict dispatches to approve vs. reject sink based on the boolean.
            .ForwardMessage<ConfidenceVerdict>("loom.patterns.review", [approve], v =>  v.Approved)
            .ForwardMessage<ConfidenceVerdict>("loom.patterns.review", [reject],  v => !v.Approved)
            .WithOutputFrom(approve)
            .WithOutputFrom(reject)
            .WithOutputFrom(infoAck)
            .WithName("LoomPatterns/06/Combined")
            .Build();

        // ── Run with checkpointing + HITL resolution ─────────────────────────
        var cm = CheckpointManager.Default;
        var checkpoints = new List<CheckpointInfo>();
        var sessionId = Guid.NewGuid().ToString("N");

        var signal = new IncidentSignal("S-6001", "checkout-api", "critical",
            "500s after 14:02 deploy — connection pool exhaustion suspected");

        Console.WriteLine($"   session    {sessionId}");
        Console.WriteLine($"   signal     {signal.Id} ({signal.Severity}) on {signal.Service}");
        Console.WriteLine($"              \"{signal.Description}\"\n");

        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow, signal, cm, sessionId: sessionId, cancellationToken: ct);

        await foreach (var evt in run.WatchStreamAsync(ct))
        {
            switch (evt)
            {
                case SuperStepCompletedEvent sse when sse.CompletionInfo?.Checkpoint is { } cp:
                    checkpoints.Add(cp);
                    Console.WriteLine($"   ⎯ checkpoint #{checkpoints.Count} (SuperStep {sse.StepNumber})");
                    break;

                case StageObserved so:
                    Console.WriteLine($"   ● event    {so.Stage}  (seen={so.SeenSoFar})");
                    break;

                case RequestInfoEvent ri when ri.Request.TryGetDataAs<SolutionPlan>(out var plan) && plan is not null:
                    Console.WriteLine($"   ? review    plan for {plan.SignalId}: \"{plan.Approach}\"");
                    Console.WriteLine("     (non-interactive demo → approving)");
                    await run.SendResponseAsync(
                        ri.Request.CreateResponse(
                            new ConfidenceVerdict(plan.SignalId, Approved: true, Reason: "auto-approve for demo")))
                        .ConfigureAwait(false);
                    break;

                case WorkflowOutputEvent wo:
                    Console.WriteLine($"\n   ★ output   {wo.Data}");
                    Console.WriteLine($"\n   checkpoints captured : {checkpoints.Count}");
                    Console.WriteLine($"   status after output  : {await run.GetStatusAsync(ct).ConfigureAwait(false)}");
                    return;
            }
        }
    }

    // ── Lifecycle event ──────────────────────────────────────────────────────

    /// <summary>Lifecycle event for observability — emitted from <see cref="StatefulIntake"/>.</summary>
    private sealed class StageObserved(string stage, int seenSoFar)
        : WorkflowEvent(data: new { stage, seenSoFar })
    {
        public string Stage { get; } = stage;
        public int SeenSoFar { get; } = seenSoFar;
    }

    // ── Stateful intake ──────────────────────────────────────────────────────

    private sealed class StatefulIntake(string id)
        : StatefulExecutor<AutofixCombinedState, IncidentSignal, IncidentSignal>(
            id,
            initialStateFactory: static () => new AutofixCombinedState(SignalsSeen: 0),
            options: new StatefulExecutorOptions { ScopeName = "loom/run" })
    {
        private IncidentSignal _pending = null!;

        public override async ValueTask<IncidentSignal> HandleAsync(
            IncidentSignal signal, IWorkflowContext ctx, CancellationToken ct = default)
        {
            await InvokeWithStateAsync(async (state, innerCtx, innerCt) =>
            {
                var updated = state with { SignalsSeen = state.SignalsSeen + 1 };
                await innerCtx.AddEventAsync(new StageObserved("intake", updated.SignalsSeen), innerCt)
                    .ConfigureAwait(false);
                _pending = signal;
                return updated;
            }, ctx, cancellationToken: ct).ConfigureAwait(false);

            return _pending;
        }
    }

    // ── Simple transformer executors ─────────────────────────────────────────

    private sealed class TriageRouter(string id) : Executor<IncidentSignal, IncidentSignal>(id)
    {
        public override ValueTask<IncidentSignal> HandleAsync(
            IncidentSignal signal, IWorkflowContext _, CancellationToken __ = default)
        {
            Console.WriteLine($"   ⊢ triage    {signal.Id} → {signal.Severity}");
            return ValueTask.FromResult(signal);
        }
    }

    /// <summary>
    ///     Trivial pass-through between the autofix subflow and the HITL port. Exists so the
    ///     port's bidirectional back-edge (port → source) lands on a regular executor whose
    ///     protocol matches the response type, rather than on the subflow binding whose entry
    ///     executor consumes a different type.
    /// </summary>
    private sealed class PlanBridge(string id) : Executor<SolutionPlan, SolutionPlan>(id)
    {
        public override ValueTask<SolutionPlan> HandleAsync(
            SolutionPlan plan, IWorkflowContext _, CancellationToken __ = default)
            => ValueTask.FromResult(plan);
    }

    private sealed class RcaStep(string id, AIAgent agent)
        : Executor<IncidentSignal, RootCauseHypothesis>(id)
    {
        public override async ValueTask<RootCauseHypothesis> HandleAsync(
            IncidentSignal signal, IWorkflowContext _, CancellationToken ct = default)
        {
            var response = await agent.RunAsync(
                $"{signal.Id} on {signal.Service}: {signal.Description}",
                cancellationToken: ct).ConfigureAwait(false);
            var hypothesis = new RootCauseHypothesis(signal.Id, response.Text, Confidence: 0.88);
            Console.WriteLine($"   ⊢ rca       {hypothesis.Hypothesis}");
            return hypothesis;
        }
    }

    private sealed class SolutionStep(string id, AIAgent agent)
        : Executor<RootCauseHypothesis, SolutionPlan>(id)
    {
        public override async ValueTask<SolutionPlan> HandleAsync(
            RootCauseHypothesis rca, IWorkflowContext _, CancellationToken ct = default)
        {
            var response = await agent.RunAsync(
                $"Plan fix for {rca.SignalId}: {rca.Hypothesis}",
                cancellationToken: ct).ConfigureAwait(false);
            var plan = new SolutionPlan(rca.SignalId, response.Text, Steps: [response.Text]);
            Console.WriteLine($"   ⊢ solution  {plan.Approach}");
            return plan;
        }
    }

    [YieldsOutput(typeof(string))]
    private sealed class InfoAcknowledge(string id) : Executor<IncidentSignal>(id)
    {
        public override async ValueTask HandleAsync(
            IncidentSignal signal, IWorkflowContext ctx, CancellationToken ct = default) =>
            await ctx.YieldOutputAsync($"ℹ INFO {signal.Id} — logged, no action taken", ct);
    }

    [YieldsOutput(typeof(string))]
    private sealed class ApprovedSink(string id) : Executor<ConfidenceVerdict>(id)
    {
        public override async ValueTask HandleAsync(
            ConfidenceVerdict v, IWorkflowContext ctx, CancellationToken ct = default) =>
            await ctx.YieldOutputAsync($"✓ APPROVED {v.SignalId} — {v.Reason}", ct);
    }

    [YieldsOutput(typeof(string))]
    private sealed class RejectedSink(string id) : Executor<ConfidenceVerdict>(id)
    {
        public override async ValueTask HandleAsync(
            ConfidenceVerdict v, IWorkflowContext ctx, CancellationToken ct = default) =>
            await ctx.YieldOutputAsync($"✗ REJECTED {v.SignalId} — {v.Reason}", ct);
    }
}
