using ANcpLua.Roslyn.Utilities.Security;

namespace Qyl.Collector.Insights;

[QylHostedService]
public sealed partial class InsightsMaterializerService(
    DuckDbStore store,
    ILogger<InsightsMaterializerService> logger,
    TimeProvider? timeProvider = null)
    : BackgroundService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), _timeProvider, stoppingToken).ConfigureAwait(false);
        await MaterializeAllAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15), _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await MaterializeAllAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogMaterializationError(logger, ex);
            }
        }
    }

    private async Task MaterializeAllAsync(CancellationToken ct)
    {
        await MaterializeTierAsync("topology",
            () => TopologyMaterializer.MaterializeAsync(store, _timeProvider, ct), ct).ConfigureAwait(false);
        await MaterializeTierAsync("profile",
            () => ProfileMaterializer.MaterializeAsync(store, _timeProvider, ct), ct).ConfigureAwait(false);
        await MaterializeTierAsync("alerts",
            () => AlertsMaterializer.MaterializeAsync(store, _timeProvider, ct), ct).ConfigureAwait(false);
    }

    private async Task MaterializeTierAsync(
        string tier, Func<Task<string>> materialize, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var content = await materialize().ConfigureAwait(false);
        sw.Stop();

        var hash = Sha256Hex.Hash(content);
        var existing = await store.GetInsightHashAsync(tier, ct).ConfigureAwait(false);

        if (hash == existing)
        {
            LogTierUnchanged(logger, tier);
            return;
        }

        var spanCount = await store.GetSpanCountAsync(ct).ConfigureAwait(false);
        await store.UpsertInsightAsync(tier, content, hash, spanCount, sw.Elapsed.TotalMilliseconds, ct)
            .ConfigureAwait(false);
        LogTierMaterialized(logger, tier, sw.Elapsed.TotalMilliseconds);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during insights materialization")]
    private static partial void LogMaterializationError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Insights tier '{Tier}' unchanged, skipping write")]
    private static partial void LogTierUnchanged(ILogger logger, string tier);

    [LoggerMessage(Level = LogLevel.Information, Message = "Materialized insights tier '{Tier}' in {DurationMs:F1}ms")]
    private static partial void LogTierMaterialized(ILogger logger, string tier, double durationMs);
}
