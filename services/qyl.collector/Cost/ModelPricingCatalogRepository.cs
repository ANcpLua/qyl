namespace Qyl.Collector.Cost;

internal enum ModelPricingCatalogAvailability
{
    Available,
    SourceUnavailable,
    Stale
}

internal sealed record ModelPricingCatalogVersion(
    string SnapshotId,
    ModelPricingCatalogSnapshot Catalog);

internal sealed record ModelPricingCatalogReadResult(
    ModelPricingCatalogAvailability Availability,
    ModelPricingCatalogVersion? Version);

internal sealed class ModelPricingCatalogRepository(
    IQylStore store,
    ModelPricingCatalogOptions options,
    TimeProvider timeProvider)
{
    public async Task<ModelPricingCatalogReadResult> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var stored = await store.GetModelPricingCatalogAsync(
                OpenRouterModelPricingCatalogSource.CatalogSourceId,
                cancellationToken)
            .ConfigureAwait(false);
        if (stored?.Source.LastVerifiedAt is not { } lastSuccessAt || stored.Models.Count is 0)
        {
            return new ModelPricingCatalogReadResult(
                ModelPricingCatalogAvailability.SourceUnavailable,
                null);
        }

        if (timeProvider.GetUtcNow() - lastSuccessAt > options.MaximumStaleAge)
            return new ModelPricingCatalogReadResult(ModelPricingCatalogAvailability.Stale, null);

        var rates = stored.Rates.ToLookup(
            static rate => (rate.ModelId, rate.TierPriority));
        var overrides = stored.Overrides.ToLookup(static priceOverride => priceOverride.ModelId);
        var models = new List<ModelPricingCatalogModel>(stored.Models.Count);
        foreach (var model in stored.Models)
        {
            var baseRates = MapRates(rates[(model.ModelId, 0)]);
            if (baseRates is null) return Unavailable();

            var modelOverrides = new List<ModelPricingOverride>();
            foreach (var priceOverride in overrides[model.ModelId].OrderBy(static value => value.Priority))
            {
                var overrideRates = MapRates(rates[(model.ModelId, priceOverride.Priority)]);
                if (overrideRates is null) return Unavailable();
                modelOverrides.Add(new ModelPricingOverride(
                    priceOverride.Priority,
                    priceOverride.ConditionUsageDimension,
                    priceOverride.ExclusiveMinimumQuantity,
                    overrideRates));
            }

            models.Add(new ModelPricingCatalogModel(
                model.ModelId,
                model.CanonicalModelId,
                model.CurrencyCode,
                baseRates,
                modelOverrides));
        }

        if (!Uri.TryCreate(stored.Snapshot.SourceEndpoint, UriKind.Absolute, out var endpoint))
            return Unavailable();

        var snapshot = new ModelPricingCatalogSnapshot(
            stored.Snapshot.SourceId,
            endpoint,
            stored.Snapshot.PriceSemantics,
            stored.Snapshot.RetrievedAt,
            models);
        if (!ModelPricingCatalogValidation.IsValid(snapshot) ||
            stored.Snapshot.ModelCount != models.Count ||
            !string.Equals(stored.Snapshot.ContentHash, stored.Snapshot.SnapshotId, StringComparison.Ordinal) ||
            !string.Equals(
                stored.Source.ActiveContentHash,
                stored.Snapshot.ContentHash,
                StringComparison.Ordinal))
        {
            return Unavailable();
        }

        return new ModelPricingCatalogReadResult(
            ModelPricingCatalogAvailability.Available,
            new ModelPricingCatalogVersion(stored.Snapshot.SnapshotId, snapshot));
    }

    private static IReadOnlyList<ModelPricingRate>? MapRates(
        IEnumerable<ModelPricingCatalogRateRow> storedRates)
    {
        var rates = new List<ModelPricingRate>();
        foreach (var rate in storedRates)
        {
            if (!Enum.TryParse<ModelPricingBillingMode>(
                    rate.BillingMode,
                    ignoreCase: false,
                    out var billingMode))
                return null;
            rates.Add(new ModelPricingRate(
                rate.SourceMeter,
                rate.UsageDimension,
                rate.Unit,
                rate.SourceBillingMode,
                billingMode,
                rate.ReplacesUsageDimension,
                rate.UsdPerUnit));
        }

        return rates;
    }

    private static ModelPricingCatalogReadResult Unavailable() =>
        new(ModelPricingCatalogAvailability.SourceUnavailable, null);
}
