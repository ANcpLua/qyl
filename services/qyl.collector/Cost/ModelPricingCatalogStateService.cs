namespace Qyl.Collector.Cost;

internal sealed record ModelPricingCatalogSourceStatus(
    string SourceId,
    int Priority,
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
    ModelPricingCatalogSourceRegistry registry,
    ModelPricingCatalogOptions options,
    IQylStore store,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ModelPricingCatalogSourceStatus>> GetSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        var stored = await store.GetModelPricingCatalogSourcesAsync(cancellationToken)
            .ConfigureAwait(false);
        var storedBySource = stored.ToDictionary(
            static state => state.Source.SourceId,
            StringComparer.Ordinal);
        var result = new List<ModelPricingCatalogSourceStatus>();
        foreach (var source in registry.Sources)
        {
            storedBySource.Remove(source.SourceId, out var state);
            result.Add(state is null
                ? new ModelPricingCatalogSourceStatus(
                    source.SourceId,
                    source.Priority,
                    source.SourceEndpoint,
                    "pending",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)
                : Map(source.Priority, state, "configured"));
        }

        foreach (var state in storedBySource.Values.OrderBy(static value => value.Source.SourceId, StringComparer.Ordinal))
            result.Add(Map(int.MaxValue, state, "unconfigured"));
        return result;
    }

    private ModelPricingCatalogSourceStatus Map(
        int priority,
        ModelPricingCatalogSourceState state,
        string configurationStatus)
    {
        var status = configurationStatus is "configured"
            ? state.Source.Status
            : configurationStatus;
        if (status is "current" &&
            (state.Source.LastVerifiedAt is not { } verifiedAt ||
             timeProvider.GetUtcNow() - verifiedAt > options.MaximumStaleAge))
        {
            status = "stale";
        }

        return new ModelPricingCatalogSourceStatus(
            state.Source.SourceId,
            priority,
            new Uri(state.Source.ConfiguredEndpoint),
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
