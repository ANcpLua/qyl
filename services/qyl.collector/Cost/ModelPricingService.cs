namespace Qyl.Collector.Cost;

[QylService(QylLifetime.Singleton)]
internal sealed partial class ModelPricingService(DuckDbStore store, ILogger<ModelPricingService> logger)
{
    private readonly Lock _lock = new();
    private FrozenDictionary<string, ModelPricingEntry> _cache = FrozenDictionary<string, ModelPricingEntry>.Empty;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var count = await store.GetModelPricingCountAsync(ct).ConfigureAwait(false);
        if (count is 0)
        {
            await SeedPricingDataAsync(ct);
        }

        await RefreshCacheAsync(ct);
    }

    public double? ComputeCost(string? provider, string? model, long? inputTokens, long? outputTokens)
    {
        if (provider is null || model is null)
            return null;

        if (inputTokens is null && outputTokens is null)
            return null;

        var key = MakeCacheKey(provider, model);
        if (!_cache.TryGetValue(key, out var pricing))
        {
            key = MakeCacheKey("*", model);
            if (!_cache.TryGetValue(key, out pricing))
                return null;
        }

        var cost = ((inputTokens ?? 0) * ((double)pricing.InputCostPerMillion / 1_000_000.0))
                   + ((outputTokens ?? 0) * ((double)pricing.OutputCostPerMillion / 1_000_000.0));

        return cost;
    }

    public SpanBatch EnrichBatchWithCost(SpanBatch batch)
    {
        var spans = batch.Spans;
        List<SpanStorageRow>? enriched = null;

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            double? cost = null;

            if (span.GenAiCostUsd is null)
            {
                cost = ComputeCost(
                    span.GenAiProviderName, span.GenAiRequestModel,
                    span.GenAiInputTokens, span.GenAiOutputTokens);
            }

            if (cost is not null && enriched is null)
            {
                enriched = new List<SpanStorageRow>(spans.Count);
                for (var j = 0; j < i; j++)
                    enriched.Add(spans[j]);
            }

            enriched?.Add(cost is not null ? span with { GenAiCostUsd = cost } : span);
        }

        if (enriched is null)
            return batch;

        return new SpanBatch(enriched);
    }

    public async Task RefreshCacheAsync(CancellationToken ct = default)
    {
        var entries = await store.GetActiveModelPricingAsync(ct).ConfigureAwait(false);
        var cache = new Dictionary<string, ModelPricingEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
            cache.TryAdd(MakeCacheKey(entry.Provider, entry.Model), entry);

        lock (_lock)
        {
            _cache = cache.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        LogPricingCacheRefreshed(cache.Count);
    }

    private async Task SeedPricingDataAsync(CancellationToken ct)
    {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "data", "model-pricing.json");
        if (!File.Exists(seedPath))
        {
            seedPath = Path.Combine("data", "model-pricing.json");
            if (!File.Exists(seedPath))
            {
                LogSeedFileNotFound(seedPath);
                return;
            }
        }

        var json = await File.ReadAllTextAsync(seedPath, ct);
        var entries = JsonSerializer.Deserialize(json, CostSerializerContext.Default.ListModelPricingSeed);
        if (entries is null or { Count: 0 })
        {
            LogSeedFileEmpty(seedPath);
            return;
        }

        await store.InsertModelPricingSeedsAsync(entries, TimeProvider.System.GetUtcNow().UtcDateTime, ct)
            .ConfigureAwait(false);

        LogSeedDataLoaded(entries.Count, seedPath);
    }

    private static string MakeCacheKey(string provider, string model) => $"{provider}::{model}";

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Pricing cache refreshed: {Count} active entries")]
    private partial void LogPricingCacheRefreshed(int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Seed pricing file not found: {Path}")]
    private partial void LogSeedFileNotFound(string path);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Seed pricing file is empty: {Path}")]
    private partial void LogSeedFileEmpty(string path);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Seeded {Count} model pricing entries from {Path}")]
    private partial void LogSeedDataLoaded(int count, string path);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(List<ModelPricingSeed>))]
internal partial class CostSerializerContext : JsonSerializerContext;
