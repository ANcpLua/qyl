// Copyright (c) 2025-2026 ancplua

using Qyl.Contracts.Observability;

namespace Qyl.Loom.Autofix;

/// <summary>
///     Background scheduler: polls the collector for pending fix runs and dispatches each to
///     <see cref="LoomAutofixRunner" />. The runner is the single-agent MAF-native pipeline —
///     no workflow graph, no per-stage executors. Scheduling, batching, and back-pressure
///     logic live here; all pipeline logic lives in the runner.
/// </summary>
[QylHostedService]
public sealed partial class AutofixAgentService(
    CollectorClient collector,
    LoomAutofixRunner runner,
    IConfiguration configuration,
    ILogger<AutofixAgentService> logger)
    : BackgroundService
{
    private readonly bool _enabled = configuration.GetValue("QYL_AUTOFIX_ENABLED", true);
    private readonly int _intervalSeconds = configuration.GetValue("QYL_AUTOFIX_INTERVAL_SECONDS", 15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            LogAutofixDisabled("QYL_AUTOFIX_ENABLED=false");
            return;
        }

        // Warm-up delay — let the triage pipeline populate pending fix runs.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);
        LogAutofixStarted(_intervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await ProcessPendingFixRunsAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    internal async Task ProcessPendingFixRunsAsync(CancellationToken ct)
    {
        var pending = await collector.GetPendingFixRunsAsync(5, ct).ConfigureAwait(false);
        if (pending.Count is 0) return;

        LogProcessingBatch(pending.Count);

        foreach (var run in pending)
        {
            await runner.RunAsync(run.RunId, ct).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Autofix agent service disabled: {Reason}")]
    private partial void LogAutofixDisabled(string reason);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Autofix agent service started (interval: {IntervalSeconds}s)")]
    private partial void LogAutofixStarted(int intervalSeconds);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Processing {Count} pending fix runs")]
    private partial void LogProcessingBatch(int count);
}
