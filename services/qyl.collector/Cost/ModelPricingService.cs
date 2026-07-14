using System.Text.Json.Serialization;

namespace Qyl.Collector.Cost;

internal sealed partial class ModelPricingService(
    IQylStore store,
    ILogger<ModelPricingService> logger,
    HttpClient? httpClient = null)
{
    // OpenRouter's public model catalog carries live USD-per-token pricing for every
    // major lab; no API key required. Prices are converted to USD per Mtoken to match
    // the model_pricing schema. The DuckDB table doubles as an offline cache: a failed
    // fetch falls back to the last stored snapshot, and an empty table means cost
    // enrichment is a no-op (GenAiCostUsd stays null — ComputeCost already tolerates it).
    private const string PricingEndpoint = "https://openrouter.ai/api/v1/models";

    private FrozenDictionary<string, ModelPricingRow> _cache = FrozenDictionary<string, ModelPricingRow>.Empty;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await FetchLivePricingAsync(ct).ConfigureAwait(false);
        await RefreshCacheAsync(ct);
    }

    public double? ComputeCost(
        string? provider, string? model, long? inputTokens, long? outputTokens,
        long? cacheReadTokens = null, long? cacheCreationTokens = null, long? reasoningTokens = null)
        => ComputeCost(
            Volatile.Read(ref _cache), provider, model, inputTokens, outputTokens,
            cacheReadTokens, cacheCreationTokens, reasoningTokens);

    private static double? ComputeCost(
        FrozenDictionary<string, ModelPricingRow> cache,
        string? provider, string? model, long? inputTokens, long? outputTokens,
        long? cacheReadTokens = null, long? cacheCreationTokens = null, long? reasoningTokens = null)
    {
        if (provider is null || model is null)
            return null;

        if (inputTokens is null && outputTokens is null
            && cacheReadTokens is null && cacheCreationTokens is null && reasoningTokens is null)
            return null;

        if (!cache.TryGetValue(MakeCacheKey(provider, model), out var pricing)
            && !cache.TryGetValue(MakeCacheKey(provider, NormalizeModel(model)), out pricing)
            && !cache.TryGetValue(MakeModelOnlyKey(NormalizeModel(model)), out pricing))
            return null;

        // Token classes are disjoint (Anthropic/OpenAI report cached-read, cache-creation and
        // reasoning tokens separately from plain input/output), so costs are additive. Optional
        // per-Mtoken rates fall back to the plain input/output rate when a model omits them.
        var cost = ((inputTokens ?? 0) * PerToken(pricing.InputCost))
                   + ((outputTokens ?? 0) * PerToken(pricing.OutputCost))
                   + ((cacheReadTokens ?? 0) * PerToken(pricing.CacheReadCost ?? pricing.InputCost))
                   + ((cacheCreationTokens ?? 0) * PerToken(pricing.CacheWriteCost ?? pricing.InputCost))
                   + ((reasoningTokens ?? 0) * PerToken(pricing.ReasoningCost ?? pricing.OutputCost));

        return cost;
    }

    private static double PerToken(decimal costPerMillion) => (double)costPerMillion / 1_000_000.0;

    public SpanBatch EnrichBatchWithCost(SpanBatch batch)
    {
        var cache = Volatile.Read(ref _cache);
        var spans = batch.Spans;
        List<SpanStorageRow>? enriched = null;

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            var cost = ComputeCost(
                cache,
                span.GenAiProviderName, span.GenAiRequestModel,
                span.GenAiInputTokens, span.GenAiOutputTokens,
                span.GenAiCacheReadInputTokens, span.GenAiCacheCreationInputTokens, span.GenAiReasoningTokens);

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
        var cache = new Dictionary<string, ModelPricingRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            // Entries arrive newest-first, and TryAdd keeps the first row per key, so a
            // newer snapshot always wins. Alias keys absorb the naming drift between wire
            // attributes (gen_ai.request.model like "claude-sonnet-4-5-20250514") and
            // catalog ids ("anthropic/claude-sonnet-4.5").
            cache.TryAdd(MakeCacheKey(entry.Provider, entry.Model), entry);
            cache.TryAdd(MakeCacheKey(entry.Provider, NormalizeModel(entry.Model)), entry);
            cache.TryAdd(MakeModelOnlyKey(NormalizeModel(entry.Model)), entry);
        }

        Volatile.Write(ref _cache, cache.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));

        LogPricingCacheRefreshed(cache.Count);
    }

    private async Task FetchLivePricingAsync(CancellationToken ct)
    {
        List<ModelPricingRow> fetched;
        try
        {
            var client = httpClient ?? SharedClient;
            using var response = await client.GetAsync(PricingEndpoint, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var catalog = await JsonSerializer.DeserializeAsync(
                body, CostSerializerContext.Default.OpenRouterCatalog, ct).ConfigureAwait(false);
            fetched = MapCatalog(catalog);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            LogLivePricingFetchFailed(ex, PricingEndpoint);
            return;
        }

        if (fetched.Count == 0)
        {
            LogLivePricingEmpty(PricingEndpoint);
            return;
        }

        // Only write a new snapshot when prices actually changed; otherwise every
        // startup would append a full duplicate generation to model_pricing.
        var current = await store.GetActiveModelPricingAsync(ct).ConfigureAwait(false);
        if (SnapshotEquals(current, fetched))
            return;

        await store.InsertModelPricingSeedsAsync(fetched, TimeProvider.System.GetUtcNow(), ct)
            .ConfigureAwait(false);
        LogLivePricingStored(fetched.Count, PricingEndpoint);
    }

    private static List<ModelPricingRow> MapCatalog(OpenRouterCatalog? catalog)
    {
        var rows = new List<ModelPricingRow>();
        if (catalog?.Data is null)
            return rows;

        foreach (var model in catalog.Data)
        {
            if (model.Id is null || model.Pricing is null)
                continue;

            var slash = model.Id.IndexOf('/');
            if (slash <= 0 || slash == model.Id.Length - 1)
                continue;

            if (!TryPerMillion(model.Pricing.Prompt, out var input)
                || !TryPerMillion(model.Pricing.Completion, out var output))
                continue;

            rows.Add(new ModelPricingRow
            {
                Provider = model.Id[..slash],
                Model = model.Id[(slash + 1)..],
                InputCost = input,
                OutputCost = output,
                CacheReadCost = TryPerMillion(model.Pricing.InputCacheRead, out var cr) ? cr : null,
                CacheWriteCost = TryPerMillion(model.Pricing.InputCacheWrite, out var cw) ? cw : null,
                ReasoningCost = TryPerMillion(model.Pricing.InternalReasoning, out var rz) ? rz : null,
            });
        }

        return rows;
    }

    private static bool TryPerMillion(string? usdPerToken, out decimal perMillion)
    {
        perMillion = 0m;
        if (!decimal.TryParse(usdPerToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var perToken)
            || perToken <= 0m)
            return false;

        perMillion = perToken * 1_000_000m;
        return true;
    }

    private static bool SnapshotEquals(IReadOnlyList<ModelPricingRow> current, List<ModelPricingRow> fetched)
    {
        var currentByKey = new Dictionary<string, ModelPricingRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in current)
            currentByKey.TryAdd(MakeCacheKey(row.Provider, row.Model), row);

        if (currentByKey.Count != fetched.Count)
            return false;

        foreach (var row in fetched)
        {
            if (!currentByKey.TryGetValue(MakeCacheKey(row.Provider, row.Model), out var existing)
                || existing.InputCost != row.InputCost
                || existing.OutputCost != row.OutputCost
                || existing.CacheReadCost != row.CacheReadCost
                || existing.CacheWriteCost != row.CacheWriteCost
                || existing.ReasoningCost != row.ReasoningCost)
                return false;
        }

        return true;
    }

    private static string MakeCacheKey(string provider, string model) => $"{provider}::{model}";

    private static string MakeModelOnlyKey(string model) => $"*::{model}";

    /// <summary>
    /// Collapses the naming drift between catalog ids and wire model names:
    /// lowercases, strips OpenRouter variant suffixes (<c>:free</c>), strips
    /// trailing release-date stamps (<c>-20250514</c>), and folds <c>.</c> to
    /// <c>-</c> so <c>claude-sonnet-4.5</c> matches <c>claude-sonnet-4-5</c>.
    /// </summary>
    private static string NormalizeModel(string model)
    {
        var normalized = model.ToLowerInvariant();

        var colon = normalized.IndexOf(':');
        if (colon > 0)
            normalized = normalized[..colon];

        if (normalized.Length > 9
            && normalized[^9] == '-'
            && long.TryParse(normalized[^8..], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            normalized = normalized[..^9];

        return normalized.Replace('.', '-');
    }

    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Pricing cache refreshed: {Count} active entries")]
    private partial void LogPricingCacheRefreshed(int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Live pricing fetch failed from {Endpoint}; using last stored snapshot")]
    private partial void LogLivePricingFetchFailed(Exception exception, string endpoint);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Live pricing catalog from {Endpoint} contained no usable entries")]
    private partial void LogLivePricingEmpty(string endpoint);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Stored {Count} live pricing entries from {Endpoint}")]
    private partial void LogLivePricingStored(int count, string endpoint);
}

internal sealed class OpenRouterCatalog
{
    [JsonPropertyName("data")] public List<OpenRouterModel>? Data { get; init; }
}

internal sealed class OpenRouterModel
{
    [JsonPropertyName("id")] public string? Id { get; init; }

    [JsonPropertyName("pricing")] public OpenRouterPricing? Pricing { get; init; }
}

internal sealed class OpenRouterPricing
{
    [JsonPropertyName("prompt")] public string? Prompt { get; init; }

    [JsonPropertyName("completion")] public string? Completion { get; init; }

    [JsonPropertyName("input_cache_read")] public string? InputCacheRead { get; init; }

    [JsonPropertyName("input_cache_write")] public string? InputCacheWrite { get; init; }

    [JsonPropertyName("internal_reasoning")] public string? InternalReasoning { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(OpenRouterCatalog))]
internal partial class CostSerializerContext : JsonSerializerContext;
