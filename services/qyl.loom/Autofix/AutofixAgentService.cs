// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Qyl.Contracts.Observability;
using Qyl.Loom.Autofix.Workflow;

namespace Qyl.Loom;

/// <summary>
///     Scheduler-only: polls the collector for pending fix runs and dispatches each run to a freshly built
///     MAF workflow via <see cref="AutofixWorkflowFactory" />. All pipeline logic lives in the executors
///     composed in that factory.
/// </summary>
[QylHostedService]
public sealed partial class AutofixAgentService(
    CollectorClient collector,
    AutofixOrchestrator orchestrator,
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<AutofixAgentService> logger,
    IChatClient? llm = null)
    : BackgroundService
{
    private readonly bool _enabled = configuration.GetValue("QYL_AUTOFIX_ENABLED", true);
    private readonly int _intervalSeconds = configuration.GetValue("QYL_AUTOFIX_INTERVAL_SECONDS", 15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled || llm is null)
        {
            LogAutofixDisabled(llm is null ? "no LLM configured" : "QYL_AUTOFIX_ENABLED=false");
            return;
        }

        // Warmup delay — let triage pipeline populate pending fix runs
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
            await DispatchRunAsync(run, ct).ConfigureAwait(false);
        }
    }

    private async Task DispatchRunAsync(FixRunRecord run, CancellationToken ct)
    {
        var workflow = AutofixWorkflowFactory.Create(services);

        try
        {
            var streamingRun = await InProcessExecution
                .RunStreamingAsync(
                    workflow,
                    new StartAutofix(run.RunId),
                    CheckpointManager.Default,
                    sessionId: run.RunId,
                    cancellationToken: ct)
                .ConfigureAwait(false);

            await using (streamingRun.ConfigureAwait(false))
            {
                await foreach (var evt in streamingRun.WatchStreamAsync(ct).ConfigureAwait(false))
                {
                    LogWorkflowEvent(run.RunId, evt.GetType().Name);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            await MarkRunFailedAsync(run, ex, ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            await MarkRunFailedAsync(run, ex, ct).ConfigureAwait(false);
        }
    }

    private async Task MarkRunFailedAsync(FixRunRecord run, Exception ex, CancellationToken ct)
    {
        LogFixRunFailed(run.RunId, ex);
        await orchestrator
            .UpdateFixRunStatusAsync(run.IssueId, run.RunId, "failed", ex.Message, ct: ct)
            .ConfigureAwait(false);
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

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Fix run {RunId} workflow event: {EventType}")]
    private partial void LogWorkflowEvent(string runId, string eventType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Fix run {RunId} failed")]
    private partial void LogFixRunFailed(string runId, Exception ex);
}
