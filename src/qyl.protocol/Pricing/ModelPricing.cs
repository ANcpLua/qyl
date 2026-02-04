// =============================================================================
// qyl.protocol - Model Pricing Tables
// Static lookup for LLM pricing per million tokens
// BCL-only - no external dependencies
// =============================================================================

using System.Collections.Frozen;

namespace qyl.protocol.Pricing;

/// <summary>
///     Model pricing per million tokens (MTok).
/// </summary>
/// <param name="InputPerMTok">Cost per million input tokens in USD.</param>
/// <param name="OutputPerMTok">Cost per million output tokens in USD.</param>
/// <param name="ContextWindow">Maximum context window size in tokens.</param>
public readonly record struct ModelPricing(
    decimal InputPerMTok,
    decimal OutputPerMTok,
    int ContextWindow);

/// <summary>
///     Static lookup tables for LLM model pricing.
///     Prices in USD per million tokens. Updated January 2026.
/// </summary>
public static class ModelPricingTable
{
    private static readonly FrozenDictionary<(string Provider, string Model), ModelPricing> s_all =
        CreatePricingTable();
    private static readonly ProviderModelComparer s_providerModelComparer = new();
    private static readonly StringComparer s_providerComparer = StringComparer.OrdinalIgnoreCase;

    private static FrozenDictionary<(string Provider, string Model), ModelPricing> CreatePricingTable()
    {
        var entries = new Dictionary<(string Provider, string Model), ModelPricing>(s_providerModelComparer);

        // OpenAI
        AddModel(entries, "openai", "gpt-4o", new(2.50m, 10.00m, 128_000));
        AddModel(entries, "openai", "gpt-4o-2024-11-20", new(2.50m, 10.00m, 128_000));
        AddModel(entries, "openai", "gpt-4o-mini", new(0.15m, 0.60m, 128_000));
        AddModel(entries, "openai", "gpt-4o-mini-2024-07-18", new(0.15m, 0.60m, 128_000));
        AddModel(entries, "openai", "gpt-4.1", new(2.00m, 8.00m, 1_000_000));
        AddModel(entries, "openai", "gpt-4.1-2025-04-14", new(2.00m, 8.00m, 1_000_000));
        AddModel(entries, "openai", "gpt-4.1-mini", new(0.40m, 1.60m, 1_000_000));
        AddModel(entries, "openai", "gpt-4.1-nano", new(0.10m, 0.40m, 1_000_000));
        AddModel(entries, "openai", "gpt-4-turbo", new(10.00m, 30.00m, 128_000));
        AddModel(entries, "openai", "gpt-4-turbo-2024-04-09", new(10.00m, 30.00m, 128_000));
        AddModel(entries, "openai", "gpt-4-turbo-preview", new(10.00m, 30.00m, 128_000));
        AddModel(entries, "openai", "gpt-4", new(30.00m, 60.00m, 8_192));
        AddModel(entries, "openai", "gpt-4-32k", new(60.00m, 120.00m, 32_768));
        AddModel(entries, "openai", "gpt-3.5-turbo", new(0.50m, 1.50m, 16_385));
        AddModel(entries, "openai", "gpt-3.5-turbo-0125", new(0.50m, 1.50m, 16_385));
        AddModel(entries, "openai", "gpt-3.5-turbo-instruct", new(1.50m, 2.00m, 4_096));
        AddModel(entries, "openai", "o1", new(15.00m, 60.00m, 200_000));
        AddModel(entries, "openai", "o1-2024-12-17", new(15.00m, 60.00m, 200_000));
        AddModel(entries, "openai", "o1-mini", new(1.10m, 4.40m, 128_000));
        AddModel(entries, "openai", "o1-preview", new(15.00m, 60.00m, 128_000));
        AddModel(entries, "openai", "o3", new(2.00m, 8.00m, 200_000));
        AddModel(entries, "openai", "o3-mini", new(1.10m, 4.40m, 200_000));
        AddModel(entries, "openai", "o4-mini", new(1.10m, 4.40m, 200_000));

        // Anthropic
        AddModel(entries, "anthropic", "claude-opus-4-5-20251101", new(5.00m, 25.00m, 200_000));
        AddModel(entries, "anthropic", "claude-opus-4.5", new(5.00m, 25.00m, 200_000));
        AddModel(entries, "anthropic", "claude-sonnet-4-5-20251101", new(3.00m, 15.00m, 200_000));
        AddModel(entries, "anthropic", "claude-sonnet-4.5", new(3.00m, 15.00m, 200_000));
        AddModel(entries, "anthropic", "claude-haiku-4-5-20251101", new(1.00m, 5.00m, 200_000));
        AddModel(entries, "anthropic", "claude-haiku-4.5", new(1.00m, 5.00m, 200_000));
        AddModel(entries, "anthropic", "claude-opus-4-1-20250501", new(15.00m, 75.00m, 200_000));
        AddModel(entries, "anthropic", "claude-opus-4.1", new(15.00m, 75.00m, 200_000));
        AddModel(entries, "anthropic", "claude-opus-4-20250514", new(15.00m, 75.00m, 200_000));
        AddModel(entries, "anthropic", "claude-opus-4", new(15.00m, 75.00m, 200_000));
        AddModel(entries, "anthropic", "claude-sonnet-4-20250514", new(3.00m, 15.00m, 200_000));
        AddModel(entries, "anthropic", "claude-sonnet-4", new(3.00m, 15.00m, 200_000));
        AddModel(entries, "anthropic", "claude-3-5-sonnet-20241022", new(3.00m, 15.00m, 200_000));
        AddModel(entries, "anthropic", "claude-3.5-sonnet", new(3.00m, 15.00m, 200_000));
        AddModel(entries, "anthropic", "claude-3-5-haiku-20241022", new(0.80m, 4.00m, 200_000));
        AddModel(entries, "anthropic", "claude-haiku-3.5", new(0.80m, 4.00m, 200_000));
        AddModel(entries, "anthropic", "claude-3-opus-20240229", new(15.00m, 75.00m, 200_000));
        AddModel(entries, "anthropic", "claude-3-sonnet-20240229", new(3.00m, 15.00m, 200_000));
        AddModel(entries, "anthropic", "claude-3-haiku-20240307", new(0.25m, 1.25m, 200_000));
        AddModel(entries, "anthropic", "claude-3-haiku", new(0.25m, 1.25m, 200_000));

        // Google
        AddModel(entries, "google", "gemini-3-pro-preview", new(2.00m, 12.00m, 1_000_000));
        AddModel(entries, "google", "gemini-3-flash-preview", new(0.50m, 3.00m, 1_000_000));
        AddModel(entries, "google", "gemini-2.5-pro", new(1.25m, 10.00m, 1_000_000));
        AddModel(entries, "google", "gemini-2.5-pro-preview-0514", new(1.25m, 10.00m, 1_000_000));
        AddModel(entries, "google", "gemini-2.5-flash", new(0.30m, 2.50m, 1_000_000));
        AddModel(entries, "google", "gemini-2.5-flash-preview-0514", new(0.30m, 2.50m, 1_000_000));
        AddModel(entries, "google", "gemini-2.5-flash-lite", new(0.10m, 0.40m, 1_000_000));
        AddModel(entries, "google", "gemini-2.0-flash", new(0.10m, 0.40m, 1_000_000));
        AddModel(entries, "google", "gemini-2.0-flash-exp", new(0.10m, 0.40m, 1_000_000));
        AddModel(entries, "google", "gemini-2.0-flash-lite", new(0.075m, 0.30m, 1_000_000));
        AddModel(entries, "google", "gemini-1.5-pro", new(1.25m, 5.00m, 1_000_000));
        AddModel(entries, "google", "gemini-1.5-flash", new(0.075m, 0.30m, 1_000_000));

        // Mistral
        AddModel(entries, "mistral", "mistral-large-3", new(0.50m, 1.50m, 131_072));
        AddModel(entries, "mistral", "mistral-large-2512", new(0.50m, 1.50m, 131_072));
        AddModel(entries, "mistral", "mistral-large-2411", new(2.00m, 6.00m, 131_072));
        AddModel(entries, "mistral", "mistral-large-latest", new(0.50m, 1.50m, 131_072));
        AddModel(entries, "mistral", "mistral-medium-3", new(0.40m, 2.00m, 131_072));
        AddModel(entries, "mistral", "mistral-medium-3.1", new(0.40m, 2.00m, 131_072));
        AddModel(entries, "mistral", "mistral-medium-latest", new(0.40m, 2.00m, 131_072));
        AddModel(entries, "mistral", "mistral-small-3.1", new(0.03m, 0.11m, 32_768));
        AddModel(entries, "mistral", "mistral-small-3.2", new(0.06m, 0.18m, 32_768));
        AddModel(entries, "mistral", "mistral-small-latest", new(0.06m, 0.18m, 32_768));
        AddModel(entries, "mistral", "codestral", new(0.30m, 0.90m, 256_000));
        AddModel(entries, "mistral", "codestral-2508", new(0.30m, 0.90m, 256_000));
        AddModel(entries, "mistral", "codestral-latest", new(0.30m, 0.90m, 256_000));
        AddModel(entries, "mistral", "devstral-small", new(0.06m, 0.12m, 128_000));
        AddModel(entries, "mistral", "devstral-small-2505", new(0.06m, 0.12m, 128_000));
        AddModel(entries, "mistral", "devstral-medium", new(0.40m, 2.00m, 128_000));
        AddModel(entries, "mistral", "mistral-nemo", new(0.02m, 0.07m, 128_000));
        AddModel(entries, "mistral", "open-mistral-nemo", new(0.02m, 0.07m, 128_000));
        AddModel(entries, "mistral", "pixtral-large", new(2.00m, 6.00m, 128_000));
        AddModel(entries, "mistral", "pixtral-12b", new(0.15m, 0.15m, 128_000));

        // Ollama (local - free)
        AddModel(entries, "ollama", "llama3.3", new(0m, 0m, 128_000));
        AddModel(entries, "ollama", "llama3.2", new(0m, 0m, 128_000));
        AddModel(entries, "ollama", "llama3.1", new(0m, 0m, 128_000));
        AddModel(entries, "ollama", "llama3", new(0m, 0m, 8_192));
        AddModel(entries, "ollama", "mistral", new(0m, 0m, 32_768));
        AddModel(entries, "ollama", "mixtral", new(0m, 0m, 32_768));
        AddModel(entries, "ollama", "codellama", new(0m, 0m, 16_384));
        AddModel(entries, "ollama", "deepseek-coder", new(0m, 0m, 16_384));
        AddModel(entries, "ollama", "phi3", new(0m, 0m, 128_000));
        AddModel(entries, "ollama", "qwen2.5", new(0m, 0m, 128_000));

        return entries.ToFrozenDictionary(s_providerModelComparer);

        static void AddModel(
            Dictionary<(string Provider, string Model), ModelPricing> dict,
            string provider,
            string model,
            ModelPricing pricing) =>
            dict[(provider, model)] = pricing;
    }

    /// <summary>
    ///     Normalizes provider name to canonical form for lookup.
    /// </summary>
    private static string NormalizeProvider(string provider)
    {
        if (s_providerComparer.Equals(provider, "azure") ||
            s_providerComparer.Equals(provider, "azure_openai") ||
            s_providerComparer.Equals(provider, "azure.ai.openai"))
            return "openai";

        if (s_providerComparer.Equals(provider, "gemini") ||
            s_providerComparer.Equals(provider, "gcp.gemini") ||
            s_providerComparer.Equals(provider, "gcp.vertex_ai"))
            return "google";

        if (s_providerComparer.Equals(provider, "mistral_ai") ||
            s_providerComparer.Equals(provider, "mistralai"))
            return "mistral";

        return provider;
    }

    /// <summary>
    ///     Gets pricing for a model by provider name.
    /// </summary>
    /// <param name="provider">Provider name (openai, anthropic, google, mistral, ollama).</param>
    /// <param name="model">Model name.</param>
    /// <returns>Pricing if found, null otherwise.</returns>
    public static ModelPricing? GetPricing(string? provider, string? model)
    {
        if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(model))
            return null;

        return s_all.GetValueOrDefault((NormalizeProvider(provider), model));
    }

    /// <summary>
    ///     Gets all model names available for a provider.
    /// </summary>
    /// <param name="provider">Provider name.</param>
    /// <returns>Enumerable of model names for the provider.</returns>
    public static IEnumerable<string> GetModelsForProvider(string provider)
    {
        var normalized = NormalizeProvider(provider);
        return s_all.Keys
            .Where(k => s_providerComparer.Equals(k.Provider, normalized))
            .Select(k => k.Model);
    }

    private sealed class ProviderModelComparer : IEqualityComparer<(string Provider, string Model)>
    {
        public bool Equals((string Provider, string Model) x, (string Provider, string Model) y)
        {
            return s_providerComparer.Equals(x.Provider, y.Provider) &&
                s_providerComparer.Equals(x.Model, y.Model);
        }

        public int GetHashCode((string Provider, string Model) obj)
        {
            var providerHash = s_providerComparer.GetHashCode(obj.Provider ?? string.Empty);
            var modelHash = s_providerComparer.GetHashCode(obj.Model ?? string.Empty);
            return unchecked((providerHash * 397) ^ modelHash);
        }
    }

    /// <summary>
    ///     Calculates cost in USD for token usage.
    /// </summary>
    /// <param name="pricing">Model pricing.</param>
    /// <param name="inputTokens">Number of input tokens.</param>
    /// <param name="outputTokens">Number of output tokens.</param>
    /// <returns>Total cost in USD.</returns>
    public static decimal CalculateCost(ModelPricing pricing, long inputTokens, long outputTokens)
    {
        var inputCost = inputTokens / 1_000_000m * pricing.InputPerMTok;
        var outputCost = outputTokens / 1_000_000m * pricing.OutputPerMTok;
        return inputCost + outputCost;
    }

    /// <summary>
    ///     Calculates cost in USD for token usage, with null-safe lookup.
    /// </summary>
    /// <param name="provider">Provider name.</param>
    /// <param name="model">Model name.</param>
    /// <param name="inputTokens">Number of input tokens.</param>
    /// <param name="outputTokens">Number of output tokens.</param>
    /// <returns>Total cost in USD, or null if pricing not found.</returns>
    public static decimal? CalculateCost(string? provider, string? model, long inputTokens, long outputTokens)
    {
        var pricing = GetPricing(provider, model);
        return pricing.HasValue ? CalculateCost(pricing.Value, inputTokens, outputTokens) : null;
    }

    /// <summary>
    ///     Calculates context utilization as a percentage.
    /// </summary>
    /// <param name="pricing">Model pricing with context window.</param>
    /// <param name="inputTokens">Number of input tokens used.</param>
    /// <returns>Context utilization (0.0 - 1.0).</returns>
    public static double CalculateContextUtilization(ModelPricing pricing, long inputTokens)
    {
        if (pricing.ContextWindow <= 0) return 0;
        return (double)inputTokens / pricing.ContextWindow;
    }

    /// <summary>
    ///     Gets the context window size for a model.
    /// </summary>
    /// <param name="provider">Provider name.</param>
    /// <param name="model">Model name.</param>
    /// <returns>Context window in tokens, or null if not found.</returns>
    public static int? GetContextWindow(string? provider, string? model) => GetPricing(provider, model)?.ContextWindow;
}
