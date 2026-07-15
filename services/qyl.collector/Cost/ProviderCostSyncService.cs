namespace Qyl.Collector.Cost;

internal sealed partial class ProviderCostSyncService(
    IEnumerable<IProviderCostSource> sources,
    ProviderCostSyncOptions options,
    IQylStore store,
    TimeProvider timeProvider,
    ILogger<ProviderCostSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SyncAllAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(options.SyncInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            await SyncAllAsync(stoppingToken).ConfigureAwait(false);
    }

    internal async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var periodEnd = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var periodStart = periodEnd.AddDays(-options.LookbackDays);

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attemptedAt = timeProvider.GetUtcNow();
            var scopeKey = options.ScopeFor(source.Provider).CreateStableKey(source.Provider);
            ProviderCostFetchResult result;
            try
            {
                result = await source.FetchAsync(periodStart, periodEnd, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                result = ProviderCostFetchResult.Failed(
                    periodStart,
                    periodEnd,
                    ProviderCostFailureCategory.InvalidResponse);
            }

            if (!result.IsSuccess)
            {
                var category = result.Failure?.Category ?? ProviderCostFailureCategory.InvalidResponse;
                await store.UpsertProviderCostSyncAsync(new ProviderCostSyncRow
                {
                    ProjectId = options.ProjectId,
                    Provider = source.Provider,
                    SourceEndpoint = source.SourceEndpoint.AbsoluteUri,
                    ProviderScopeKey = scopeKey,
                    SourceKind = "actual_billed_cost",
                    Attribution = SourceAttribution(source.Provider),
                    Status = category is ProviderCostFailureCategory.MissingCredential
                        ? "unconfigured"
                        : "sync_failed",
                    LastAttemptAt = attemptedAt,
                    FailureCategory = FailureCode(category)
                }, cancellationToken).ConfigureAwait(false);

                if (category is not ProviderCostFailureCategory.MissingCredential)
                    LogProviderCostSyncFailed(logger, source.Provider, FailureCode(category));
                continue;
            }

            var buckets = Aggregate(result.Records, options.ProjectId, scopeKey);
            var coveredRange = ProviderCostCoverage.GetContiguousRange(result.CoveredPeriods);
            var successAt = result.Records.Count > 0
                ? result.Records.Max(static record => record.RetrievedAt)
                : timeProvider.GetUtcNow();

            await store.ReplaceProviderCostBucketsAsync(
                    options.ProjectId,
                    source.Provider,
                    result.PeriodStart,
                    result.PeriodEnd,
                    buckets,
                    new ProviderCostSyncRow
                    {
                        ProjectId = options.ProjectId,
                        Provider = source.Provider,
                        SourceEndpoint = source.SourceEndpoint.AbsoluteUri,
                        ProviderScopeKey = scopeKey,
                        SourceKind = "actual_billed_cost",
                        Attribution = SourceAttribution(source.Provider),
                        Status = "current",
                        LastAttemptAt = attemptedAt,
                        LastSuccessAt = successAt,
                        PeriodStart = coveredRange?.PeriodStart,
                        PeriodEnd = coveredRange?.PeriodEnd
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            LogProviderCostSyncCompleted(logger, source.Provider, buckets.Count);
        }
    }

    internal static IReadOnlyList<ProviderCostBucketRow> Aggregate(
        IReadOnlyList<ProviderCostRecord> records,
        string projectId,
        string providerScopeKey) =>
        [.. records
            .GroupBy(static record => new
            {
                record.Provider,
                record.PeriodStart,
                record.PeriodEnd,
                record.CurrencyCode,
                record.ModelName,
                record.SourceEndpoint,
                record.Attribution
            })
            .Select(group => new ProviderCostBucketRow
            {
                ProjectId = projectId,
                Provider = group.Key.Provider,
                PeriodStart = group.Key.PeriodStart,
                PeriodEnd = group.Key.PeriodEnd,
                ModelKey = group.Key.ModelName ?? "*",
                SourceEndpoint = group.Key.SourceEndpoint.AbsoluteUri,
                ProviderScopeKey = providerScopeKey,
                SourceKind = "actual_billed_cost",
                Attribution = group.Key.Attribution is ProviderCostAttribution.ProviderReportedModel
                    ? "provider_model_period"
                    : "provider_period",
                CurrencyCode = group.Key.CurrencyCode.ToUpperInvariant(),
                Amount = group.Sum(static record => record.Amount),
                RetrievedAt = group.Max(static record => record.RetrievedAt)
            })];

    private static string SourceAttribution(string provider) =>
        string.Equals(provider, "anthropic", StringComparison.Ordinal)
            ? "provider_model_period"
            : "provider_period";

    private static string FailureCode(ProviderCostFailureCategory category) => category switch
    {
        ProviderCostFailureCategory.MissingCredential => "missing_credential",
        ProviderCostFailureCategory.InvalidCredential => "invalid_credential",
        ProviderCostFailureCategory.InvalidPeriod => "invalid_period",
        ProviderCostFailureCategory.Authentication => "authentication",
        ProviderCostFailureCategory.Authorization => "authorization",
        ProviderCostFailureCategory.RateLimited => "rate_limited",
        ProviderCostFailureCategory.ProviderUnavailable => "provider_unavailable",
        ProviderCostFailureCategory.Timeout => "timeout",
        ProviderCostFailureCategory.Transport => "transport",
        ProviderCostFailureCategory.InvalidResponse => "invalid_response",
        _ => "unexpected_response_status"
    };

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Provider cost sync completed for {Provider}: {BucketCount} aggregate buckets")]
    private static partial void LogProviderCostSyncCompleted(ILogger logger, string provider, int bucketCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Provider cost sync failed for {Provider}: {FailureCategory}")]
    private static partial void LogProviderCostSyncFailed(ILogger logger, string provider, string failureCategory);
}
