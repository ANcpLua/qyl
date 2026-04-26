// Copyright (c) 2025-2026 ancplua

using Qyl.Loom.Patterns.Agents;
using Qyl.Loom.Patterns.Contracts;

namespace Qyl.Loom.Patterns.Patterns;

/// <summary>
///     Pattern 01 — <c>WorkflowBuilder.AddSwitch</c> + <c>AddCase&lt;T&gt;</c> + <c>WithDefault</c>.
///     Triages an <see cref="IncidentSignal" /> by severity and routes it to the matching
///     analyzer. Pure predicate routing — no custom router executor required.
/// </summary>
public static class Pattern01_SwitchRouting
{
    /// <summary>Runs the switch-routing demonstration end-to-end.</summary>
    public static async Task RunAsync(IQylLoomPatternsAgentsBuilder agents, CancellationToken ct)
    {
        var intake = new IntakeExecutor("patterns/01/intake");
        var criticalSink = new SeverityHandler("patterns/01/critical", "critical");
        var warningSink = new SeverityHandler("patterns/01/warning", "warning");
        var fallbackSink = new SeverityHandler("patterns/01/fallback", "fallback");

        var workflow = new WorkflowBuilder(intake)
            .AddSwitch(intake, sw => sw
                .AddCase<IncidentSignal>(s => s is { Severity: "critical" }, criticalSink)
                .AddCase<IncidentSignal>(s => s is { Severity: "warning" }, warningSink)
                .WithDefault(fallbackSink))
            .WithOutputFrom(criticalSink)
            .WithOutputFrom(warningSink)
            .WithOutputFrom(fallbackSink)
            .WithName("LoomPatterns/01/SwitchRouting")
            .Build();

        _ = agents; // agents unused here — routing demo is pure topology.

        IncidentSignal[] fleet =
        [
            new("S-1001", "checkout-api", "critical", "5xx spike after 14:02 deploy"),
            new("S-1002", "catalog-worker", "warning", "queue depth trending up"),
            new("S-1003", "marketing-cron", "info", "nightly report late by 4 min")
        ];

        foreach (var signal in fleet)
        {
            Console.WriteLine($"\n── routing signal {signal.Id} ({signal.Severity}) ──");
            await using var run = await InProcessExecution.RunStreamingAsync(
                workflow, signal, cancellationToken: ct).ConfigureAwait(false);
            await foreach (var evt in run.WatchStreamAsync(ct))
            {
                if (evt is WorkflowOutputEvent wo)
                {
                    Console.WriteLine($"   ★ output   {wo.Data}");
                    break;
                }
            }
        }
    }

    // ── Executors ────────────────────────────────────────────────────────────

    /// <summary>
    ///     Intake — logs the signal and forwards it unchanged so AddSwitch can pattern-match.
    /// </summary>
    private sealed class IntakeExecutor(string id) : Executor<IncidentSignal, IncidentSignal>(id)
    {
        public override ValueTask<IncidentSignal> HandleAsync(
            IncidentSignal signal, IWorkflowContext _, CancellationToken __ = default)
        {
            Console.WriteLine($"   ▶ intake    {signal.Id} ({signal.Service}) — \"{signal.Description}\"");
            return ValueTask.FromResult(signal);
        }
    }

    /// <summary>
    ///     Terminal branch handler — prints which severity bucket matched and yields the
    ///     bucket name as the workflow output.
    /// </summary>
    [YieldsOutput(typeof(string))]
    private sealed class SeverityHandler(string id, string severity) : Executor<IncidentSignal>(id)
    {
        public override async ValueTask HandleAsync(
            IncidentSignal signal, IWorkflowContext ctx, CancellationToken ct = default)
        {
            Console.WriteLine($"   ⊢ {severity,-9} {signal.Id} routed here");
            await ctx.YieldOutputAsync($"{signal.Id} → {severity}", ct);
        }
    }
}
