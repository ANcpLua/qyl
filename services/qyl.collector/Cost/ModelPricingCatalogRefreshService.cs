namespace Qyl.Collector.Cost;

internal enum ModelPricingCatalogRefreshReason
{
    Startup,
    TimeToLiveExpired,
    Manual
}

internal sealed record ModelPricingCatalogRefreshSignal(
    string SourceId,
    ModelPricingCatalogRefreshReason Reason);

internal sealed record ModelPricingCatalogChangedEventArgs(
    string SourceId,
    string SnapshotId,
    Uri SourceEndpoint,
    string PriceSemantics,
    DateTimeOffset RetrievedAt,
    int ModelCount);

internal sealed partial class ModelPricingCatalogRefreshService(
    ModelPricingCatalogSourceRegistry registry,
    ModelPricingCatalogOptions options,
    IQylStore store,
    TimeProvider timeProvider,
    ILogger<ModelPricingCatalogRefreshService> logger) : BackgroundService
{
    private readonly Channel<ModelPricingCatalogRefreshSignal> _requests =
        Channel.CreateUnbounded<ModelPricingCatalogRefreshSignal>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private readonly ConcurrentDictionary<string, byte> _queued = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    internal event EventHandler<ModelPricingCatalogChangedEventArgs>? SnapshotChanged;

    public override void Dispose()
    {
        _refreshGate.Dispose();
        base.Dispose();
    }

    internal bool RequestRefresh(
        string sourceId,
        ModelPricingCatalogRefreshReason reason = ModelPricingCatalogRefreshReason.Manual)
    {
        if (!registry.Sources.Any(source => string.Equals(source.SourceId, sourceId, StringComparison.Ordinal)))
            return false;
        if (!_queued.TryAdd(sourceId, 0)) return true;
        if (_requests.Writer.TryWrite(new ModelPricingCatalogRefreshSignal(sourceId, reason))) return true;
        _queued.TryRemove(sourceId, out _);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        QueueAll(ModelPricingCatalogRefreshReason.Startup);
        var expiryTask = QueueExpiryEventsAsync(stoppingToken);
        try
        {
            await foreach (var request in _requests.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                _queued.TryRemove(request.SourceId, out _);
                var source = registry.Sources.First(
                    source => string.Equals(source.SourceId, request.SourceId, StringComparison.Ordinal));
                await RefreshSourceSerializedAsync(source, stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _requests.Writer.TryComplete();
            try
            {
                await expiryTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal hosted-service shutdown.
            }
        }
    }

    internal async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        foreach (var source in registry.Sources)
            await RefreshSourceSerializedAsync(source, cancellationToken).ConfigureAwait(false);
    }

    private async Task QueueExpiryEventsAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(options.SyncInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            QueueAll(ModelPricingCatalogRefreshReason.TimeToLiveExpired);
    }

    private void QueueAll(ModelPricingCatalogRefreshReason reason)
    {
        foreach (var source in registry.Sources)
            RequestRefresh(source.SourceId, reason);
    }

    private async Task RefreshSourceSerializedAsync(
        IModelPricingCatalogSource source,
        CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RefreshSourceAsync(source, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshSourceAsync(
        IModelPricingCatalogSource source,
        CancellationToken cancellationToken)
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
        catch (Exception)
        {
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
                options.RetainedSnapshotsPerSource,
                cancellationToken)
            .ConfigureAwait(false);

        if (!changed) return;
        PublishChanged(new ModelPricingCatalogChangedEventArgs(
            snapshot.SourceId,
            snapshotId,
            snapshot.SourceEndpoint,
            snapshot.PriceSemantics,
            snapshot.RetrievedAt,
            snapshot.Models.Count));
        LogSnapshotChanged(logger, source.SourceId, snapshotId, snapshot.Models.Count);
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

    private void PublishChanged(ModelPricingCatalogChangedEventArgs args)
    {
        if (SnapshotChanged is not { } changed) return;
        foreach (EventHandler<ModelPricingCatalogChangedEventArgs> handler in changed.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception error)
            {
                LogSubscriberFailed(logger, args.SourceId, error);
            }
        }
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
        Message = "Model-pricing catalog change subscriber failed for {SourceId}")]
    private static partial void LogSubscriberFailed(ILogger logger, string sourceId, Exception error);
}
