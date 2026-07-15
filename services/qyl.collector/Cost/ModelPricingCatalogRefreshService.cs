namespace Qyl.Collector.Cost;

internal sealed partial class ModelPricingCatalogRefreshService(
    OpenRouterModelPricingCatalogSource source,
    ModelPricingCatalogOptions options,
    IQylStore store,
    TimeProvider timeProvider,
    ILogger<ModelPricingCatalogRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(options.SyncInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            await RefreshAsync(stoppingToken).ConfigureAwait(false);
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var attemptedAt = timeProvider.GetUtcNow();
        ModelPricingCatalogFetchResult result;
        try
        {
            result = await source.FetchAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            LogUnexpectedSourceFailure(logger, source.SourceId, error);
            result = ModelPricingCatalogFetchResult.Failed(
                ModelPricingCatalogFailureCategory.InvalidResponse);
        }

        if (result.Snapshot is not { } snapshot ||
            !result.IsSuccess ||
            !string.Equals(snapshot.SourceId, source.SourceId, StringComparison.Ordinal) ||
            snapshot.SourceEndpoint != source.SourceEndpoint ||
            !ModelPricingCatalogValidation.IsValid(snapshot))
        {
            var category = result.Failure?.Category ??
                           ModelPricingCatalogFailureCategory.InvalidResponse;
            await store.UpsertModelPricingCatalogSourceAsync(new ModelPricingCatalogSourceRow
            {
                SourceId = source.SourceId,
                ConfiguredEndpoint = source.SourceEndpoint.AbsoluteUri,
                ConfiguredFingerprint = source.ConfigurationFingerprint,
                Status = "sync_failed",
                LastAttemptAt = attemptedAt,
                FailureCategory = FailureCode(category)
            }, cancellationToken).ConfigureAwait(false);
            LogSyncFailed(logger, source.SourceId, FailureCode(category));
            return;
        }

        var snapshotId = ModelPricingCatalogSnapshotIdentity.Compute(
            snapshot,
            source.ConfigurationFingerprint);
        var models = new List<ModelPricingCatalogModelRow>(snapshot.Models.Count);
        var overrides = new List<ModelPricingCatalogOverrideRow>();
        var rates = new List<ModelPricingCatalogRateRow>();
        foreach (var model in snapshot.Models)
        {
            models.Add(new ModelPricingCatalogModelRow
            {
                SourceId = snapshot.SourceId,
                SnapshotId = snapshotId,
                ModelId = model.ModelId,
                CanonicalModelId = model.CanonicalModelId,
                CurrencyCode = model.CurrencyCode
            });
            AddRates(rates, snapshot.SourceId, snapshotId, model.ModelId, 0, model.Rates);

            foreach (var priceOverride in model.Overrides)
            {
                overrides.Add(new ModelPricingCatalogOverrideRow
                {
                    SourceId = snapshot.SourceId,
                    SnapshotId = snapshotId,
                    ModelId = model.ModelId,
                    Priority = priceOverride.Priority,
                    ConditionUsageDimension = priceOverride.ConditionUsageDimension,
                    ExclusiveMinimumQuantity = priceOverride.ExclusiveMinimumQuantity
                });
                AddRates(
                    rates,
                    snapshot.SourceId,
                    snapshotId,
                    model.ModelId,
                    priceOverride.Priority,
                    priceOverride.Rates);
            }
        }

        var changed = await store.ActivateModelPricingCatalogSnapshotAsync(
                new ModelPricingCatalogSourceRow
                {
                    SourceId = snapshot.SourceId,
                    ConfiguredEndpoint = source.SourceEndpoint.AbsoluteUri,
                    ConfiguredFingerprint = source.ConfigurationFingerprint,
                    ActiveSnapshotId = snapshotId,
                    ActiveConfigurationFingerprint = source.ConfigurationFingerprint,
                    ActiveContentHash = snapshotId,
                    Status = "current",
                    LastAttemptAt = attemptedAt,
                    LastVerifiedAt = snapshot.RetrievedAt
                },
                new ModelPricingCatalogSnapshotRow
                {
                    SourceId = snapshot.SourceId,
                    SnapshotId = snapshotId,
                    ContentHash = snapshotId,
                    ConfigurationFingerprint = source.ConfigurationFingerprint,
                    SourceEndpoint = snapshot.SourceEndpoint.AbsoluteUri,
                    PriceSemantics = snapshot.PriceSemantics,
                    ModelCount = snapshot.Models.Count,
                    RetrievedAt = snapshot.RetrievedAt
                },
                models,
                overrides,
                rates,
                options.RetainedSnapshots,
                cancellationToken)
            .ConfigureAwait(false);

        if (changed) LogSnapshotChanged(logger, source.SourceId, snapshotId, snapshot.Models.Count);
    }

    private static void AddRates(
        List<ModelPricingCatalogRateRow> destination,
        string sourceId,
        string snapshotId,
        string modelId,
        int tierPriority,
        IReadOnlyList<ModelPricingRate> rates)
    {
        destination.AddRange(rates.Select(rate => new ModelPricingCatalogRateRow
        {
            SourceId = sourceId,
            SnapshotId = snapshotId,
            ModelId = modelId,
            TierPriority = tierPriority,
            SourceMeter = rate.SourceMeter,
            UsageDimension = rate.UsageDimension,
            Unit = rate.Unit,
            SourceBillingMode = rate.SourceBillingMode,
            BillingMode = rate.BillingMode.ToString(),
            ReplacesUsageDimension = rate.ReplacesUsageDimension,
            UsdPerUnit = rate.UsdPerUnit
        }));
    }

    private static string FailureCode(ModelPricingCatalogFailureCategory category) => category switch
    {
        ModelPricingCatalogFailureCategory.Authentication => "authentication",
        ModelPricingCatalogFailureCategory.Authorization => "authorization",
        ModelPricingCatalogFailureCategory.RateLimited => "rate_limited",
        ModelPricingCatalogFailureCategory.ProviderUnavailable => "provider_unavailable",
        ModelPricingCatalogFailureCategory.Timeout => "timeout",
        ModelPricingCatalogFailureCategory.Transport => "transport",
        ModelPricingCatalogFailureCategory.InvalidResponse => "invalid_response",
        _ => "unexpected_response_status"
    };

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Model-pricing catalog activated {SnapshotId} from {SourceId}: {ModelCount} models")]
    private static partial void LogSnapshotChanged(
        ILogger logger,
        string sourceId,
        string snapshotId,
        int modelCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Model-pricing catalog refresh failed for {SourceId}: {FailureCategory}")]
    private static partial void LogSyncFailed(ILogger logger, string sourceId, string failureCategory);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Model-pricing catalog source threw unexpectedly for {SourceId}")]
    private static partial void LogUnexpectedSourceFailure(ILogger logger, string sourceId, Exception error);
}
