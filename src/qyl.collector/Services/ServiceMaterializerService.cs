namespace qyl.collector.Services;

/// <summary>
///     Background service that recomputes service_instances aggregates
///     from spans/logs tables every 5 minutes.
/// </summary>
public sealed partial class ServiceMaterializerService(
    DuckDbStore store,
    ILogger<ServiceMaterializerService> logger,
    TimeProvider? timeProvider = null)
    : BackgroundService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), _timeProvider, stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await MaterializeAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogMaterializationError(ex);
            }
        }
    }

    private async Task MaterializeAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        await store.BackfillMissingServicesAsync(ct).ConfigureAwait(false);
        await store.UpdateServiceAggregatesAsync(ct).ConfigureAwait(false);

        sw.Stop();
        LogMaterialized(sw.Elapsed.TotalMilliseconds);
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error during service registry materialization")]
    private partial void LogMaterializationError(Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Service registry materialized in {DurationMs:F1}ms")]
    private partial void LogMaterialized(double durationMs);
}
