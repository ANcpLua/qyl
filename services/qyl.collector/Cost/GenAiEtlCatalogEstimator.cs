using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Collector.Cost;

internal enum GenAiEtlObservedModelIdentityBasis
{
    ResponseModel,
    RequestModelFallback
}

internal sealed record GenAiEtlCatalogPriceProvenance(
    string SourceId,
    Uri SourceEndpoint,
    string SnapshotId,
    string PriceModelId,
    string ObservedModelId,
    GenAiEtlObservedModelIdentityBasis ObservedModelIdentityBasis,
    ModelPricingMatchKind ModelMatchKind,
    DateTimeOffset RetrievedAt,
    string PriceSemantics);

internal sealed record GenAiEtlCatalogEstimateResult(
    ModelPricingEstimateStatus Status,
    long PricedCallCount,
    decimal? EstimatedTokenCostUsd,
    decimal? EstimatedTokenCostPerCallUsd,
    GenAiEtlCatalogPriceProvenance? Provenance,
    IReadOnlyList<ModelPricingEstimateComponent> Components,
    IReadOnlyList<ModelPricingEstimateExclusion> Exclusions);

internal sealed record GenAiEtlCatalogSourceRead(
    string SourceId,
    int Priority,
    ModelPricingCatalogReadResult Catalog);

internal sealed class GenAiEtlCatalogEstimator(
    ModelPricingCatalogRepository repository,
    ModelPricingCatalogSourceRegistry registry)
{
    public async Task<IReadOnlyList<GenAiEtlCatalogEstimateResult>> EstimateAsync(
        IReadOnlyList<GenAiEtlAuditStorageRow> rows,
        IReadOnlyList<GenAiEtlAuditUsageBucket> usageBuckets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(usageBuckets);

        var reads = await Task.WhenAll(registry.Sources.Select(async source =>
            new GenAiEtlCatalogSourceRead(
                source.SourceId,
                source.Priority,
                await repository.GetAsync(source.SourceId, cancellationToken).ConfigureAwait(false))))
            .ConfigureAwait(false);

        return Estimate(rows, usageBuckets, reads);
    }

    internal static IReadOnlyList<GenAiEtlCatalogEstimateResult> Estimate(
        IReadOnlyList<GenAiEtlAuditStorageRow> rows,
        IReadOnlyList<GenAiEtlAuditUsageBucket> usageBuckets,
        IReadOnlyList<GenAiEtlCatalogSourceRead> sources)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(usageBuckets);
        ArgumentNullException.ThrowIfNull(sources);

        var orderedSources = sources
            .OrderBy(static source => source.Priority)
            .ThenBy(static source => source.SourceId, StringComparer.Ordinal)
            .ToArray();
        var bucketsByCluster = usageBuckets
            .GroupBy(static bucket => ClusterKey.From(bucket))
            .ToDictionary(static group => group.Key, static group => group.ToArray());

        var estimates = new GenAiEtlCatalogEstimateResult[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            bucketsByCluster.TryGetValue(ClusterKey.From(row), out var clusterBuckets);
            estimates[index] = EstimateCluster(row, clusterBuckets ?? [], orderedSources);
        }

        return estimates;
    }

    private static GenAiEtlCatalogEstimateResult EstimateCluster(
        GenAiEtlAuditStorageRow row,
        IReadOnlyList<GenAiEtlAuditUsageBucket> buckets,
        IReadOnlyList<GenAiEtlCatalogSourceRead> sources)
    {
        var observedModel = row.ModelName;
        if (!ModelPricingCatalogValidation.IsText(observedModel, 512) ||
            !TryMapIdentityBasis(row.ModelIdentityBasis, out var identityBasis))
        {
            return Failure(ModelPricingEstimateStatus.MissingModelIdentity);
        }

        if (!HasCompleteUsage(row, buckets, out var usageFailure))
            return Failure(usageFailure);

        var firstUsage = CreateUsage(buckets[0]);
        var hasAvailableCatalog = false;
        var hasStaleCatalog = false;
        foreach (var source in sources)
        {
            var read = source.Catalog;
            if (read.Availability is ModelPricingCatalogAvailability.Stale)
            {
                hasStaleCatalog = true;
                continue;
            }

            if (read.Availability is not ModelPricingCatalogAvailability.Available || read.Version is null)
                continue;

            hasAvailableCatalog = true;
            var firstEstimate = CalculateBucket(
                read.Version,
                observedModel!,
                row.OutputType,
                firstUsage);
            if (firstEstimate.Status is ModelPricingEstimateStatus.ModelNotFound)
                continue;

            // Once the highest-priority available source resolves (or ambiguously resolves)
            // the exact observed identity, its outcome owns the cluster. Calculation failures
            // must not select a more convenient or cheaper lower-priority catalog.
            return AggregateOwnedSource(
                row,
                buckets,
                read.Version,
                identityBasis,
                observedModel!,
                firstEstimate);
        }

        return Failure(hasAvailableCatalog
            ? ModelPricingEstimateStatus.ModelNotFound
            : hasStaleCatalog
                ? ModelPricingEstimateStatus.StaleSource
                : ModelPricingEstimateStatus.SourceUnavailable);
    }

    private static GenAiEtlCatalogEstimateResult AggregateOwnedSource(
        GenAiEtlAuditStorageRow row,
        IReadOnlyList<GenAiEtlAuditUsageBucket> buckets,
        ModelPricingCatalogVersion version,
        GenAiEtlObservedModelIdentityBasis identityBasis,
        string observedModel,
        ModelPricingEstimateResult firstEstimate)
    {
        var componentTotals = new Dictionary<ComponentKey, ComponentTotal>();
        var exclusions = new HashSet<ModelPricingEstimateExclusion>();
        decimal total = 0;

        try
        {
            for (var index = 0; index < buckets.Count; index++)
            {
                var bucket = buckets[index];
                var estimate = index is 0
                    ? firstEstimate
                    : CalculateBucket(
                        version,
                        observedModel,
                        row.OutputType,
                        CreateUsage(bucket));
                foreach (var exclusion in estimate.Exclusions)
                    exclusions.Add(exclusion);

                if (estimate.Status is not ModelPricingEstimateStatus.Calculated ||
                    estimate.TokenCostUsd is not { } bucketCost ||
                    !string.Equals(estimate.CurrencyCode, "USD", StringComparison.Ordinal))
                {
                    return Failure(
                        estimate.Status is ModelPricingEstimateStatus.Calculated
                            ? ModelPricingEstimateStatus.UnsupportedPricing
                            : estimate.Status,
                        exclusions);
                }

                var multiplier = checked((decimal)bucket.CallCount);
                total = checked(total + checked(bucketCost * multiplier));
                foreach (var component in estimate.Components)
                {
                    var key = ComponentKey.From(component);
                    componentTotals.TryGetValue(key, out var accumulated);
                    componentTotals[key] = new ComponentTotal(
                        checked(accumulated.Quantity + checked(component.Quantity * multiplier)),
                        checked(accumulated.AmountUsd + checked(component.AmountUsd * multiplier)));
                }
            }

            if (componentTotals.Count is 0)
                return Failure(ModelPricingEstimateStatus.UnsupportedPricing, exclusions);

            var components = componentTotals
                .Select(static pair => pair.Key.ToComponent(pair.Value))
                .OrderBy(static component => component.SourceMeter, StringComparer.Ordinal)
                .ThenBy(static component => component.UsageDimension, StringComparer.Ordinal)
                .ThenBy(static component => component.RateRelation)
                .ThenBy(static component => component.UsdPerUnit)
                .ThenBy(static component => component.OverrideEvidence?.SourceOrder ?? 0)
                .ThenBy(static component => component.OverrideEvidence?.ObservedQuantity ?? 0)
                .ToArray();
            var orderedExclusions = OrderExclusions(exclusions);
            var modelMatchKind = firstEstimate.MatchKind;
            if (firstEstimate.MatchedModelId is not { } priceModelId ||
                modelMatchKind is null ||
                row.CallCount <= 0)
            {
                return Failure(ModelPricingEstimateStatus.UnsupportedPricing, orderedExclusions);
            }

            return new GenAiEtlCatalogEstimateResult(
                ModelPricingEstimateStatus.Calculated,
                row.CallCount,
                total,
                checked(total / row.CallCount),
                new GenAiEtlCatalogPriceProvenance(
                    version.Catalog.SourceId,
                    version.Catalog.SourceEndpoint,
                    version.SnapshotId,
                    priceModelId,
                    observedModel,
                    identityBasis,
                    modelMatchKind.Value,
                    version.Catalog.RetrievedAt,
                    version.Catalog.PriceSemantics),
                components,
                orderedExclusions);
        }
        catch (OverflowException)
        {
            return Failure(ModelPricingEstimateStatus.UnsupportedPricing, exclusions);
        }
    }

    private static ModelPricingEstimateResult CalculateBucket(
        ModelPricingCatalogVersion version,
        string observedModel,
        string? outputType,
        ModelPricingUsage initialUsage)
    {
        var estimate = ModelPricingCalculator.Calculate(
            version,
            observedModel,
            initialUsage,
            aggregateCallCount: 1);
        if (estimate.Status is not ModelPricingEstimateStatus.Calculated ||
            !IsUnsupportedOutputType(outputType))
        {
            return estimate;
        }

        // This estimator intentionally owns only observed token and request units. A source
        // adapter may expose other meters, but there is no provider-neutral catalog metadata
        // today that proves which meter and quantity price a non-text output. Fail
        // closed instead of embedding provider meter aliases in this generic calculation path.
        return new ModelPricingEstimateResult(
            ModelPricingEstimateStatus.UnsupportedPricing,
            null,
            null,
            version.Catalog.SourceId,
            version.SnapshotId,
            version.Catalog.PriceSemantics,
            estimate.MatchedModelId,
            estimate.MatchKind,
            [],
            []);
    }

    private static bool IsUnsupportedOutputType(string? outputType) =>
        !string.IsNullOrWhiteSpace(outputType) &&
        outputType is not (GenAiAttributes.OutputTypeValues.Text or GenAiAttributes.OutputTypeValues.Json);

    private static ModelPricingUsage CreateUsage(GenAiEtlAuditUsageBucket bucket) =>
        ModelPricingUsage.ForGenAiCall(
            bucket.InputTokens,
            string.Equals(
                bucket.OperationName,
                GenAiAttributes.OperationNameValues.Embeddings,
                StringComparison.Ordinal)
                ? bucket.OutputTokens ?? 0
                : bucket.OutputTokens,
            bucket.CacheReadInputTokens,
            bucket.CacheCreationInputTokens,
            bucket.ReasoningOutputTokens);

    private static bool HasCompleteUsage(
        GenAiEtlAuditStorageRow row,
        IReadOnlyList<GenAiEtlAuditUsageBucket> buckets,
        out ModelPricingEstimateStatus failure)
    {
        failure = ModelPricingEstimateStatus.IncompleteUsage;
        if (row.CallCount <= 0 || buckets.Count is 0)
        {
            return false;
        }

        long bucketCallCount = 0;
        try
        {
            foreach (var bucket in buckets)
            {
                if (bucket.CallCount <= 0)
                    return false;
                bucketCallCount = checked(bucketCallCount + bucket.CallCount);
                switch (bucket.Eligibility)
                {
                    case GenAiEtlAuditUsageEligibility.Eligible:
                        break;
                    case GenAiEtlAuditUsageEligibility.UnsupportedOperation:
                        failure = ModelPricingEstimateStatus.UnsupportedPricing;
                        return false;
                    case GenAiEtlAuditUsageEligibility.MissingRequiredUsage:
                    case GenAiEtlAuditUsageEligibility.InvalidUsage:
                    default:
                        return false;
                }
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        return bucketCallCount == row.CallCount && row.TokenUsageCallCount == row.CallCount;
    }

    private static bool TryMapIdentityBasis(
        string? value,
        out GenAiEtlObservedModelIdentityBasis basis)
    {
        basis = value switch
        {
            "response_model" => GenAiEtlObservedModelIdentityBasis.ResponseModel,
            "request_model_fallback" => GenAiEtlObservedModelIdentityBasis.RequestModelFallback,
            _ => default
        };
        return value is "response_model" or "request_model_fallback";
    }

    private static GenAiEtlCatalogEstimateResult Failure(
        ModelPricingEstimateStatus status,
        IEnumerable<ModelPricingEstimateExclusion>? exclusions = null) =>
        new(
            status,
            0,
            null,
            null,
            null,
            [],
            exclusions is null ? [] : OrderExclusions(exclusions));

    private static IReadOnlyList<ModelPricingEstimateExclusion> OrderExclusions(
        IEnumerable<ModelPricingEstimateExclusion> exclusions) =>
        exclusions
            .Distinct()
            .OrderBy(static exclusion => exclusion.SourceMeter, StringComparer.Ordinal)
            .ThenBy(static exclusion => exclusion.Reason, StringComparer.Ordinal)
            .ThenBy(static exclusion => exclusion.UsageDimension, StringComparer.Ordinal)
            .ThenBy(static exclusion => exclusion.UsdPerUnit)
            .ThenBy(static exclusion => exclusion.OverrideEvidence?.SourceOrder ?? 0)
            .ThenBy(static exclusion => exclusion.OverrideEvidence?.ObservedQuantity ?? 0)
            .ToArray();

    private readonly record struct ClusterKey(
        string ServiceName,
        string? OperationName,
        string? OutputType,
        string? ProviderName,
        string? ModelName,
        string? ModelIdentityBasis)
    {
        public static ClusterKey From(GenAiEtlAuditStorageRow row) =>
            new(
                row.ServiceName,
                row.OperationName,
                row.OutputType,
                row.ProviderName,
                row.ModelName,
                row.ModelIdentityBasis);

        public static ClusterKey From(GenAiEtlAuditUsageBucket bucket) =>
            new(
                bucket.ServiceName,
                bucket.OperationName,
                bucket.OutputType,
                bucket.ProviderName,
                bucket.ModelName,
                bucket.ModelIdentityBasis);
    }

    private readonly record struct ComponentTotal(decimal Quantity, decimal AmountUsd);

    private readonly record struct ComponentKey(
        string SourceMeter,
        string UsageDimension,
        string Unit,
        string SourceBillingMode,
        ModelPricingBillingMode BillingMode,
        ModelPricingRateRelation RateRelation,
        string? ReplacesUsageDimension,
        decimal UsdPerUnit,
        ModelPricingOverrideEvidence? OverrideEvidence)
    {
        public static ComponentKey From(ModelPricingEstimateComponent component) =>
            new(
                component.SourceMeter,
                component.UsageDimension,
                component.Unit,
                component.SourceBillingMode,
                component.BillingMode,
                component.RateRelation,
                component.ReplacesUsageDimension,
                component.UsdPerUnit,
                component.OverrideEvidence);

        public ModelPricingEstimateComponent ToComponent(ComponentTotal total) =>
            new(
                SourceMeter,
                UsageDimension,
                Unit,
                SourceBillingMode,
                BillingMode,
                RateRelation,
                ReplacesUsageDimension,
                total.Quantity,
                UsdPerUnit,
                total.AmountUsd,
                OverrideEvidence);
    }
}
