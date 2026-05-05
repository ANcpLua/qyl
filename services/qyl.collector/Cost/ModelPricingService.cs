namespace Qyl.Collector.Cost;

[QylService(QylLifetime.Singleton)]
public sealed partial class ModelPricingService(DuckDbStore store, ILogger<ModelPricingService> logger)
{
    private readonly Lock _lock = new();
    private FrozenDictionary<string, PricingEntry> _cache = FrozenDictionary<string, PricingEntry>.Empty;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var count = await GetPricingCountAsync(ct);
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
        var entries = new Dictionary<string, PricingEntry>(StringComparer.OrdinalIgnoreCase);

        await using var lease = await store.GetReadConnectionAsync(ct);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT provider, model, input_cost, output_cost, reasoning_cost,
                                 cache_read_cost, cache_write_cost
                          FROM model_pricing
                          WHERE valid_to IS NULL
                          ORDER BY valid_from DESC
                          """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var provider = reader.GetString(0);
            var model = reader.GetString(1);
            var entry = new PricingEntry(
                reader.GetDecimal(2),
                reader.GetDecimal(3),
                await reader.IsDBNullAsync(4, ct) ? null : reader.GetDecimal(4),
                await reader.IsDBNullAsync(5, ct) ? null : reader.GetDecimal(5),
                await reader.IsDBNullAsync(6, ct) ? null : reader.GetDecimal(6));

            var key = MakeCacheKey(provider, model);
            entries.TryAdd(key, entry);
        }

        lock (_lock)
        {
            _cache = entries.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        LogPricingCacheRefreshed(entries.Count);
    }

    private async Task<long> GetPricingCountAsync(CancellationToken ct)
    {
        await using var lease = await store.GetReadConnectionAsync(ct);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM model_pricing";
        var result = await cmd.ExecuteScalarAsync(ct);
        return result switch
        {
            long v => v,
            int v => v,
            _ => 0
        };
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
        var entries = JsonSerializer.Deserialize(json, CostSerializerContext.Default.ListSeedPricingEntry);
        if (entries is null or { Count: 0 })
        {
            LogSeedFileEmpty(seedPath);
            return;
        }

        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, wct) =>
        {
            foreach (var entry in entries)
            {
                await using var cmd = con.CreateCommand();
                cmd.CommandText = """
                                  INSERT INTO model_pricing
                                      (provider, model, input_cost, output_cost, reasoning_cost,
                                       cache_read_cost, cache_write_cost, valid_from)
                                  VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                                  ON CONFLICT DO NOTHING
                                  """;
                cmd.Parameters.Add(new DuckDBParameter { Value = entry.Provider });
                cmd.Parameters.Add(new DuckDBParameter { Value = entry.Model });
                cmd.Parameters.Add(new DuckDBParameter { Value = entry.InputCost });
                cmd.Parameters.Add(new DuckDBParameter { Value = entry.OutputCost });
                cmd.Parameters.Add(new DuckDBParameter { Value = (object?)entry.ReasoningCost ?? DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = (object?)entry.CacheReadCost ?? DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = (object?)entry.CacheWriteCost ?? DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = now });
                await cmd.ExecuteNonQueryAsync(wct).ConfigureAwait(false);
            }
        }, ct);

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

public sealed record PricingEntry(
    decimal InputCostPerMillion,
    decimal OutputCostPerMillion,
    decimal? ReasoningCostPerMillion,
    decimal? CacheReadCostPerMillion,
    decimal? CacheWriteCostPerMillion);

public sealed class SeedPricingEntry
{
    [JsonPropertyName("provider")] public required string Provider { get; init; }

    [JsonPropertyName("model")] public required string Model { get; init; }

    [JsonPropertyName("input_cost")] public required decimal InputCost { get; init; }

    [JsonPropertyName("output_cost")] public required decimal OutputCost { get; init; }

    [JsonPropertyName("reasoning_cost")] public decimal? ReasoningCost { get; init; }

    [JsonPropertyName("cache_read_cost")] public decimal? CacheReadCost { get; init; }

    [JsonPropertyName("cache_write_cost")] public decimal? CacheWriteCost { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(List<SeedPricingEntry>))]
internal partial class CostSerializerContext : JsonSerializerContext;
