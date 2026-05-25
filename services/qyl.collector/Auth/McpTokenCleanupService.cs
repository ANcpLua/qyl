namespace Qyl.Collector.Auth;

internal sealed partial class McpTokenCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private readonly IMcpTokenStore _store;
    private readonly ILogger<McpTokenCleanupService> _logger;
    private readonly TimeProvider _timeProvider;

    public McpTokenCleanupService(
        IMcpTokenStore store,
        ILogger<McpTokenCleanupService> logger,
        TimeProvider timeProvider)
    {
        _store = store;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger, CleanupInterval);

        using var timer = new PeriodicTimer(CleanupInterval, _timeProvider);

        do
        {
            try
            {
                var deleted = await _store.CleanupExpiredAsync(stoppingToken).ConfigureAwait(false);
                if (deleted > 0)
                {
                    LogCleanedUp(_logger, deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is DuckDB.NET.Data.DuckDBException or IOException or InvalidOperationException)
            {
                LogCleanupFailed(_logger, ex);
            }
        } while (await WaitForNextTickAsync(timer, stoppingToken).ConfigureAwait(false));
    }

    private static async ValueTask<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "MCP token cleanup service started — interval={Interval}")]
    private static partial void LogStarted(ILogger logger, TimeSpan interval);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Cleaned up {DeletedCount} expired MCP tokens")]
    private static partial void LogCleanedUp(ILogger logger, int deletedCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "MCP token cleanup failed")]
    private static partial void LogCleanupFailed(ILogger logger, Exception ex);
}
