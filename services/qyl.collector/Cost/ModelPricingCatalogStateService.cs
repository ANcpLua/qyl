namespace Qyl.Collector.Cost;

internal sealed record ModelPricingCatalogSourceStatus(
    string SourceId,
    Uri Endpoint,
    string Status,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastVerifiedAt,
    DateTimeOffset? RetrievedAt,
    string? SnapshotId,
    string? PriceSemantics,
    int? ModelCount,
    string? FailureCategory);

internal sealed class ModelPricingCatalogStateService(
    OpenRouterModelPricingCatalogSource source,
    ModelPricingCatalogOptions options,
    IQylStore store,
    TimeProvider timeProvider)
{
    public async Task<ModelPricingCatalogSourceStatus> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var state = await store.GetModelPricingCatalogSourceAsync(source.SourceId, cancellationToken)
            .ConfigureAwait(false);
        if (state is null ||
            !string.Equals(
                state.Source.ConfiguredFingerprint,
                source.ConfigurationFingerprint,
                StringComparison.Ordinal))
        {
            return new ModelPricingCatalogSourceStatus(
                source.SourceId,
                source.SourceEndpoint,
                "pending",
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        return Map(state);
    }

    private ModelPricingCatalogSourceStatus Map(ModelPricingCatalogSourceState state)
    {
        var status = state.Source.Status;
        if (status is "current" &&
            (state.Source.LastVerifiedAt is not { } verifiedAt ||
             timeProvider.GetUtcNow() - verifiedAt > options.MaximumStaleAge))
        {
            status = "stale";
        }

        return new ModelPricingCatalogSourceStatus(
            source.SourceId,
            source.SourceEndpoint,
            status,
            state.Source.LastAttemptAt,
            state.Source.LastVerifiedAt,
            state.ActiveSnapshot?.RetrievedAt,
            state.ActiveSnapshot?.SnapshotId,
            state.ActiveSnapshot?.PriceSemantics,
            state.ActiveSnapshot?.ModelCount,
            state.Source.FailureCategory);
    }
}
