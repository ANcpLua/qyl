namespace Qyl.Collector.Auth;

internal sealed partial class McpTokenCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private readonly IMcpTokenStore _tokenStore;
    private readonly IPkceStateStore _pkceStore;
    private readonly ILogger<McpTokenCleanupService> _logger;
    private readonly TimeProvider _timeProvider;

    public McpTokenCleanupService(
        IMcpTokenStore tokenStore,
        IPkceStateStore pkceStore,
        ILogger<McpTokenCleanupService> logger,
        TimeProvider timeProvider)
    {
        _tokenStore = tokenStore;
        _pkceStore = pkceStore;
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
                var deletedTokens = await _tokenStore.CleanupExpiredAsync(stoppingToken).ConfigureAwait(false);
                if (deletedTokens > 0)
                {
                    LogTokensCleanedUp(_logger, deletedTokens);
                }

                var deletedPkce = await _pkceStore.CleanupExpiredAsync(stoppingToken).ConfigureAwait(false);
                if (deletedPkce > 0)
                {
                    LogPkceCleanedUp(_logger, deletedPkce);
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
    private static partial void LogTokensCleanedUp(ILogger logger, int deletedCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "MCP token cleanup failed")]
    private static partial void LogCleanupFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Cleaned up {DeletedCount} expired PKCE state rows")]
    private static partial void LogPkceCleanedUp(ILogger logger, int deletedCount);
}
