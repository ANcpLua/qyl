using System.Diagnostics;

namespace Qyl.Collector.Retention;

internal sealed partial class RetentionService(
    IQylStore store,
    RetentionOptions options,
    TimeProvider timeProvider,
    ILogger<RetentionService> logger) : BackgroundService
{
    private const int DeleteBatchSize = 1000;

    private static readonly TimeSpan s_initialDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_shutdownCheckpointTimeout = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.IsEnabled)
            return;

        try
        {
            await Task.Delay(s_initialDelay, timeProvider, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogCycleFailed(logger, ex);
            }

            try
            {
                await Task.Delay(options.Interval, timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    internal async Task<RetentionCycleOutcome> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!options.IsEnabled)
            return RetentionCycleOutcome.Empty;

        var startedAt = Stopwatch.GetTimestamp();
        var fileBefore = store.GetStorageFileMetrics();
        long deletedLogs = 0;
        long deletedSpans = 0;
        var writeBatches = 0;

        try
        {
            var cutoff = QylTimeConversions.ToUnixNanoUnsigned(
                timeProvider.GetUtcNow().AddDays(-options.Days));

            while (true)
            {
                var deleted = await store.DeleteExpiredLogsBatchAsync(
                        cutoff,
                        DeleteBatchSize,
                        cancellationToken)
                    .ConfigureAwait(false);
                writeBatches++;
                deletedLogs += deleted;
                if (deleted < DeleteBatchSize)
                    break;
            }

            while (true)
            {
                var deleted = await store.DeleteExpiredSpansBatchAsync(
                        cutoff,
                        DeleteBatchSize,
                        cancellationToken)
                    .ConfigureAwait(false);
                writeBatches++;
                deletedSpans += deleted;
                if (deleted is 0)
                    break;
            }
        }
        finally
        {
            if (deletedLogs + deletedSpans > 0)
                await CheckpointAfterDeletionAsync(cancellationToken).ConfigureAwait(false);
        }

        var duration = Stopwatch.GetElapsedTime(startedAt);
        var fileAfter = store.GetStorageFileMetrics();
        var result = new RetentionCycleOutcome(
            deletedLogs,
            deletedSpans,
            writeBatches,
            duration,
            fileBefore.DatabaseFileSizeBytes,
            fileAfter.DatabaseFileSizeBytes);

        if (result.TotalRows > 0)
        {
            LogCycleCompleted(
                logger,
                result.DeletedLogRows,
                result.DeletedSpanRows,
                result.Duration.TotalMilliseconds,
                result.FileSizeBeforeBytes,
                result.FileSizeAfterBytes);
        }

        return result;
    }

    private async Task CheckpointAfterDeletionAsync(CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            await store.CheckpointAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        using var checkpointTimeout = new CancellationTokenSource(s_shutdownCheckpointTimeout);
        await store.CheckpointAsync(checkpointTimeout.Token).ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Retention cycle deleted {DeletedLogRows} log rows and {DeletedSpanRows} span rows in {DurationMs} ms; database file size changed from {FileSizeBeforeBytes} to {FileSizeAfterBytes} bytes")]
    private static partial void LogCycleCompleted(
        ILogger logger,
        long deletedLogRows,
        long deletedSpanRows,
        double durationMs,
        long fileSizeBeforeBytes,
        long fileSizeAfterBytes);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "Retention cycle failed")]
    private static partial void LogCycleFailed(ILogger logger, Exception exception);
}

internal readonly record struct RetentionCycleOutcome(
    long DeletedLogRows,
    long DeletedSpanRows,
    int WriteBatches,
    TimeSpan Duration,
    long FileSizeBeforeBytes,
    long FileSizeAfterBytes)
{
    public static RetentionCycleOutcome Empty { get; } = new(0, 0, 0, TimeSpan.Zero, 0, 0);

    public long TotalRows => DeletedLogRows + DeletedSpanRows;
}
