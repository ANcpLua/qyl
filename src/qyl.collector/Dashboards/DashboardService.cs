namespace qyl.collector.Dashboards;

/// <summary>
///     Background service that periodically re-detects available dashboards.
///     Caches results in memory so endpoint responses are instant.
/// </summary>
public sealed partial class DashboardService(
    DashboardDetector detector,
    ILogger<DashboardService> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private volatile IReadOnlyList<DashboardDefinition> _available = [];

    /// <summary>
    ///     Returns cached list of detected dashboards.
    /// </summary>
    public IReadOnlyList<DashboardDefinition> GetAvailable() => _available;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay to let ingestion warm up
        await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken).ConfigureAwait(false);
        await DetectAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60), _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await DetectAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogDetectionError(logger, ex);
            }
        }
    }

    private async Task DetectAsync(CancellationToken ct)
    {
        var results = await detector.DetectAsync(ct).ConfigureAwait(false);
        _available = results;
        var count = results.Count(d => d.IsAvailable);
        LogDetectionComplete(logger, count);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Dashboard detection failed")]
    private static partial void LogDetectionError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dashboard detection complete: {Count} dashboards available")]
    private static partial void LogDetectionComplete(ILogger logger, int count);
}
