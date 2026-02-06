using System.Diagnostics;
using System.Text;

namespace qyl.collector.Insights;

/// <summary>
///     Background service that periodically materializes pre-computed system context
///     from DuckDB telemetry into the materialized_insights table.
///     Consumers (REST, MCP, Copilot) serve this instantly with zero query cost.
/// </summary>
public sealed partial class InsightsMaterializerService : BackgroundService
{
    private readonly DuckDbStore _store;
    private readonly ILogger<InsightsMaterializerService> _logger;
    private readonly TimeProvider _timeProvider;

    public InsightsMaterializerService(
        DuckDbStore store,
        ILogger<InsightsMaterializerService> logger,
        TimeProvider? timeProvider = null)
    {
        _store = store;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 10-second delay for ingestion warmup
        await Task.Delay(TimeSpan.FromSeconds(10), _timeProvider, stoppingToken).ConfigureAwait(false);
        await MaterializeAllAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await MaterializeAllAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogMaterializationError(_logger, ex);
            }
        }
    }

    private async Task MaterializeAllAsync(CancellationToken ct)
    {
        await MaterializeTierAsync("topology",
            () => TopologyMaterializer.MaterializeAsync(_store, _timeProvider, ct), ct).ConfigureAwait(false);
        await MaterializeTierAsync("profile",
            () => ProfileMaterializer.MaterializeAsync(_store, _timeProvider, ct), ct).ConfigureAwait(false);
        await MaterializeTierAsync("alerts",
            () => AlertsMaterializer.MaterializeAsync(_store, _timeProvider, ct), ct).ConfigureAwait(false);
    }

    private async Task MaterializeTierAsync(
        string tier, Func<Task<string>> materialize, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var content = await materialize().ConfigureAwait(false);
        sw.Stop();

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        var existing = await _store.GetInsightHashAsync(tier, ct).ConfigureAwait(false);

        if (hash == existing)
        {
            LogTierUnchanged(_logger, tier);
            return;
        }

        var spanCount = await _store.GetSpanCountAsync(ct).ConfigureAwait(false);
        await _store.UpsertInsightAsync(tier, content, hash, spanCount, sw.Elapsed.TotalMilliseconds, ct)
            .ConfigureAwait(false);
        LogTierMaterialized(_logger, tier, sw.Elapsed.TotalMilliseconds);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during insights materialization")]
    private static partial void LogMaterializationError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Insights tier '{Tier}' unchanged, skipping write")]
    private static partial void LogTierUnchanged(ILogger logger, string tier);

    [LoggerMessage(Level = LogLevel.Information, Message = "Materialized insights tier '{Tier}' in {DurationMs:F1}ms")]
    private static partial void LogTierMaterialized(ILogger logger, string tier, double durationMs);
}
