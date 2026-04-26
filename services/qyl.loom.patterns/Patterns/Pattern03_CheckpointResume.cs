// Copyright (c) 2025-2026 ancplua

using Qyl.Loom.Patterns.Agents;
using Qyl.Loom.Patterns.Contracts;

namespace Qyl.Loom.Patterns.Patterns;

/// <summary>
///     Pattern 03 — <c>CheckpointManager</c> + <c>SuperStepCompletedEvent.CompletionInfo.Checkpoint</c>
///     + <c>StreamingRun.RestoreCheckpointAsync</c>.
///     Demonstrates pause/resume by replaying the same run from a middle checkpoint after
///     initial completion. Swap <c>CheckpointManager.Default</c> for a durable subclass
///     (Cosmos, file-based) and pause/resume works across process restarts.
/// </summary>
public static class Pattern03_CheckpointResume
{
    /// <summary>Runs the checkpoint/resume demonstration end-to-end.</summary>
    public static async Task RunAsync(IQylLoomPatternsAgentsBuilder agents, CancellationToken ct)
    {
        var rca = new RcaExecutor("patterns/03/rca", agents.BuildRcaAgent());
        var solution = new SolutionExecutor("patterns/03/solution", agents.BuildSolutionAgent());
        var verdict = new VerdictExecutor("patterns/03/verdict", agents.BuildConfidenceAgent());

        var workflow = new WorkflowBuilder(rca)
            .AddEdge(rca, solution)
            .AddEdge(solution, verdict)
            .WithOutputFrom(verdict)
            .WithName("LoomPatterns/03/CheckpointResume")
            .Build();

        var cm = CheckpointManager.Default;
        var checkpoints = new List<CheckpointInfo>();

        var signal = new IncidentSignal("S-3001", "payments-gateway", "critical",
            "TLS handshake errors on Stripe webhook");

        Console.WriteLine("── initial run ──");
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, signal, cm, cancellationToken: ct);

        await foreach (var evt in run.WatchStreamAsync(ct))
        {
            switch (evt)
            {
                case SuperStepCompletedEvent sse when sse.CompletionInfo?.Checkpoint is { } cp:
                    checkpoints.Add(cp);
                    Console.WriteLine($"   ⎯ checkpoint #{checkpoints.Count} saved (SuperStep {sse.StepNumber})");
                    break;

                case WorkflowOutputEvent wo:
                    Console.WriteLine($"   ★ output   {wo.Data}");
                    break;
            }
        }

        if (checkpoints.Count < 2)
        {
            Console.WriteLine("   (not enough checkpoints to demonstrate restore)");
            return;
        }

        var restoreIndex = checkpoints.Count / 2;
        Console.WriteLine($"\n── restoring checkpoint #{restoreIndex + 1} and re-running from there ──");
        await run.RestoreCheckpointAsync(checkpoints[restoreIndex], ct).ConfigureAwait(false);

        await foreach (var evt in run.WatchStreamAsync(ct))
        {
            if (evt is WorkflowOutputEvent wo)
            {
                Console.WriteLine($"   ★ after-resume output   {wo.Data}");
                return;
            }
        }
    }

    // ── Executors ────────────────────────────────────────────────────────────

    private sealed class RcaExecutor(string id, AIAgent agent)
        : Executor<IncidentSignal, RootCauseHypothesis>(id)
    {
        public override async ValueTask<RootCauseHypothesis> HandleAsync(
            IncidentSignal signal, IWorkflowContext _, CancellationToken ct = default)
        {
            var response = await agent.RunAsync(
                $"Signal {signal.Id}: {signal.Description}", cancellationToken: ct).ConfigureAwait(false);
            var hypothesis = new RootCauseHypothesis(signal.Id, response.Text, 0.82);
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
                $"Plan fix for: {rca.Hypothesis}", cancellationToken: ct).ConfigureAwait(false);
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
                $"Review: {plan.Approach}", cancellationToken: ct).ConfigureAwait(false);
            var approved = response.Text.ContainsIgnoreCase("approve");
            await ctx.YieldOutputAsync(
                new ConfidenceVerdict(plan.SignalId, approved, response.Text), ct);
        }
    }
}
