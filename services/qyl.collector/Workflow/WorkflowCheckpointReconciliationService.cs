using Qyl.Collector.Storage;

namespace Qyl.Collector.Workflow;

// The single owner of checkpoint reconciliation scheduling.
//
// Reconciliation advances a cursor and a phase that live in the store, so two
// drivers stepping the same walk would each observe the other's partial
// progress. The store therefore never schedules itself: this service is the
// only periodic caller, which also leaves reconciliation fully deterministic
// for any caller that drives it directly.
internal sealed class WorkflowCheckpointReconciliationService(
    IQylStore store,
    ILogger<WorkflowCheckpointReconciliationService> logger) : BackgroundService
{
    private static readonly TimeSpan s_idleInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan s_continuationDelay = TimeSpan.FromMilliseconds(50);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var continuation = false;
            try
            {
                continuation = await store
                    .ReconcileWorkflowCheckpointsAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception error) when (DuckDbFailures.IsRetryable(error))
            {
                var classification = DuckDbFailures.Classify(error);
                WorkflowLifecycleLog.DuckDbFailure(
                    logger,
                    classification.ToString(),
                    retry: true,
                    error: error);
                WorkflowLifecycleLog.ReconciliationDeferred(logger, error);
            }
            catch (Exception error)
            {
                var classification = DuckDbFailures.Classify(error);
                WorkflowLifecycleLog.DuckDbFailure(
                    logger,
                    classification.ToString(),
                    retry: false,
                    error: error);
                // Checkpoint reconciliation is background maintenance. An
                // unhandled exception here would otherwise reach the host and
                // stop the collector, taking ingestion down for a fault that
                // only affects checkpoint housekeeping. It is logged at error
                // so the defect stays visible instead of being swallowed.
                WorkflowLifecycleLog.ReconciliationFailed(logger, error);
            }

            try
            {
                await Task.Delay(
                        continuation ? s_continuationDelay : s_idleInterval,
                        stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
