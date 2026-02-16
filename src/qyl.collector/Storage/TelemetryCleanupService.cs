namespace qyl.collector.Storage;

/// <summary>
///     Background service that enforces telemetry data retention limits.
///     Runs periodically to delete old data and keep storage bounded.
/// </summary>
public sealed partial class TelemetryCleanupService(
    DuckDbStore store,
    TelemetryLimitsOptions options,
    ILogger<TelemetryCleanupService> logger,
    TimeProvider? timeProvider = null)
    : BackgroundService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted(logger, options.CleanupInterval, options.MaxRetentionDays);

        using var timer = new PeriodicTimer(options.CleanupInterval, _timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await RunCleanupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCleanupError(logger, ex);
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        var cutoffTime = _timeProvider.GetUtcNow().AddDays(-options.MaxRetentionDays);
        var cutoffNanos = (ulong)(cutoffTime.ToUnixTimeMilliseconds() * 1_000_000);

        // Age-based cleanup
        var deletedByAge = await store.DeleteSpansBeforeAsync(cutoffNanos, ct).ConfigureAwait(false);
        if (deletedByAge > 0)
        {
            LogSpansDeletedByAge(logger, deletedByAge, options.MaxRetentionDays);
        }

        // Count-based cleanup for spans
        var spanCount = await store.GetSpanCountAsync(ct).ConfigureAwait(false);
        if (spanCount > options.MaxSpanCount)
        {
            var toDelete = spanCount - options.TargetSpanCount;
            var deleted = await store.DeleteOldestSpansAsync(toDelete, ct).ConfigureAwait(false);
            LogSpansDeletedByCount(logger, spanCount, options.MaxSpanCount, deleted);
        }

        // Count-based cleanup for logs
        var logCount = await store.GetLogCountAsync(ct).ConfigureAwait(false);
        if (logCount > options.MaxLogCount)
        {
            var toDelete = logCount - options.TargetLogCount;
            var deleted = await store.DeleteOldestLogsAsync(toDelete, ct).ConfigureAwait(false);
            LogLogsDeletedByCount(logger, logCount, options.MaxLogCount, deleted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Telemetry cleanup service started. Interval: {Interval}, MaxRetentionDays: {Days}")]
    private static partial void LogServiceStarted(ILogger logger, TimeSpan interval, int days);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during telemetry cleanup")]
    private static partial void LogCleanupError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} spans older than {Days} days")]
    private static partial void LogSpansDeletedByAge(ILogger logger, int count, int days);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Span count {Count} exceeded max {Max}. Deleted {Deleted} oldest spans")]
    private static partial void LogSpansDeletedByCount(ILogger logger, long count, int max, int deleted);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Log count {Count} exceeded max {Max}. Deleted {Deleted} oldest logs")]
    private static partial void LogLogsDeletedByCount(ILogger logger, long count, int max, int deleted);
}
