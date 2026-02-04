namespace qyl.collector.Storage;

/// <summary>
///     Background service that enforces telemetry data retention limits.
///     Runs periodically to delete old data and keep storage bounded.
/// </summary>
public sealed partial class TelemetryCleanupService : BackgroundService
{
    private readonly DuckDbStore _store;
    private readonly TelemetryLimitsOptions _options;
    private readonly ILogger<TelemetryCleanupService> _logger;
    private readonly TimeProvider _timeProvider;

    public TelemetryCleanupService(
        DuckDbStore store,
        TelemetryLimitsOptions options,
        ILogger<TelemetryCleanupService> logger,
        TimeProvider? timeProvider = null)
    {
        _store = store;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted(_logger, _options.CleanupInterval, _options.MaxRetentionDays);

        using var timer = new PeriodicTimer(_options.CleanupInterval, _timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await RunCleanupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCleanupError(_logger, ex);
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        var cutoffTime = _timeProvider.GetUtcNow().AddDays(-_options.MaxRetentionDays);
        var cutoffNanos = (ulong)(cutoffTime.ToUnixTimeMilliseconds() * 1_000_000);

        // Age-based cleanup
        var deletedByAge = await _store.DeleteSpansBeforeAsync(cutoffNanos, ct).ConfigureAwait(false);
        if (deletedByAge > 0)
        {
            LogSpansDeletedByAge(_logger, deletedByAge, _options.MaxRetentionDays);
        }

        // Count-based cleanup for spans
        var spanCount = await _store.GetSpanCountAsync(ct).ConfigureAwait(false);
        if (spanCount > _options.MaxSpanCount)
        {
            var toDelete = spanCount - _options.TargetSpanCount;
            var deleted = await _store.DeleteOldestSpansAsync(toDelete, ct).ConfigureAwait(false);
            LogSpansDeletedByCount(_logger, spanCount, _options.MaxSpanCount, deleted);
        }

        // Count-based cleanup for logs
        var logCount = await _store.GetLogCountAsync(ct).ConfigureAwait(false);
        if (logCount > _options.MaxLogCount)
        {
            var toDelete = logCount - _options.TargetLogCount;
            var deleted = await _store.DeleteOldestLogsAsync(toDelete, ct).ConfigureAwait(false);
            LogLogsDeletedByCount(_logger, logCount, _options.MaxLogCount, deleted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Telemetry cleanup service started. Interval: {Interval}, MaxRetentionDays: {Days}")]
    private static partial void LogServiceStarted(ILogger logger, TimeSpan interval, int days);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during telemetry cleanup")]
    private static partial void LogCleanupError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} spans older than {Days} days")]
    private static partial void LogSpansDeletedByAge(ILogger logger, int count, int days);

    [LoggerMessage(Level = LogLevel.Information, Message = "Span count {Count} exceeded max {Max}. Deleted {Deleted} oldest spans")]
    private static partial void LogSpansDeletedByCount(ILogger logger, long count, int max, int deleted);

    [LoggerMessage(Level = LogLevel.Information, Message = "Log count {Count} exceeded max {Max}. Deleted {Deleted} oldest logs")]
    private static partial void LogLogsDeletedByCount(ILogger logger, long count, int max, int deleted);
}
