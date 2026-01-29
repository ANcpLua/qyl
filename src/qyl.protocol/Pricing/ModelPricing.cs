// =============================================================================
// qyl.protocol - Model Pricing Tables
// Static lookup for LLM pricing per million tokens
// BCL-only - no external dependencies
// =============================================================================

namespace qyl.protocol.Pricing;

/// <summary>
/// Model pricing per million tokens (MTok).
/// </summary>
/// <param name="InputPerMTok">Cost per million input tokens in USD.</param>
/// <param name="OutputPerMTok">Cost per million output tokens in USD.</param>
/// <param name="ContextWindow">Maximum context window size in tokens.</param>
public readonly record struct ModelPricing(
    decimal InputPerMTok,
    decimal OutputPerMTok,
    int ContextWindow);

/// <summary>
/// Static lookup tables for LLM model pricing.
/// Prices in USD per million tokens. Updated January 2026.
/// </summary>
public static class ModelPricingTable
{
    // ═══════════════════════════════════════════════════════════════════════
    // OpenAI
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, ModelPricing> s_openAi = new(StringComparer.OrdinalIgnoreCase)
    {
        // GPT-4o family
        ["gpt-4o"] = new(2.50m, 10.00m, 128_000),
        ["gpt-4o-2024-11-20"] = new(2.50m, 10.00m, 128_000),
        ["gpt-4o-mini"] = new(0.15m, 0.60m, 128_000),
        ["gpt-4o-mini-2024-07-18"] = new(0.15m, 0.60m, 128_000),

        // GPT-4.1
        ["gpt-4.1"] = new(2.00m, 8.00m, 1_000_000),
        ["gpt-4.1-2025-04-14"] = new(2.00m, 8.00m, 1_000_000),
        ["gpt-4.1-mini"] = new(0.40m, 1.60m, 1_000_000),
        ["gpt-4.1-nano"] = new(0.10m, 0.40m, 1_000_000),

        // GPT-4 Turbo
        ["gpt-4-turbo"] = new(10.00m, 30.00m, 128_000),
        ["gpt-4-turbo-2024-04-09"] = new(10.00m, 30.00m, 128_000),
        ["gpt-4-turbo-preview"] = new(10.00m, 30.00m, 128_000),

        // GPT-4 (original)
        ["gpt-4"] = new(30.00m, 60.00m, 8_192),
        ["gpt-4-32k"] = new(60.00m, 120.00m, 32_768),

        // GPT-3.5 Turbo
        ["gpt-3.5-turbo"] = new(0.50m, 1.50m, 16_385),
        ["gpt-3.5-turbo-0125"] = new(0.50m, 1.50m, 16_385),
        ["gpt-3.5-turbo-instruct"] = new(1.50m, 2.00m, 4_096),

        // O-series reasoning models
        ["o1"] = new(15.00m, 60.00m, 200_000),
        ["o1-2024-12-17"] = new(15.00m, 60.00m, 200_000),
        ["o1-mini"] = new(1.10m, 4.40m, 128_000),
        ["o1-preview"] = new(15.00m, 60.00m, 128_000),
        ["o3"] = new(2.00m, 8.00m, 200_000),
        ["o3-mini"] = new(1.10m, 4.40m, 200_000),
        ["o4-mini"] = new(1.10m, 4.40m, 200_000),
    };

    /// <summary>OpenAI model pricing.</summary>
    public static IReadOnlyDictionary<string, ModelPricing> OpenAI => s_openAi;

    // ═══════════════════════════════════════════════════════════════════════
    // Anthropic
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, ModelPricing> s_anthropic = new(StringComparer.OrdinalIgnoreCase)
    {
        // Claude 4.5
        ["claude-opus-4-5-20251101"] = new(5.00m, 25.00m, 200_000),
        ["claude-opus-4.5"] = new(5.00m, 25.00m, 200_000),
        ["claude-sonnet-4-5-20251101"] = new(3.00m, 15.00m, 200_000),
        ["claude-sonnet-4.5"] = new(3.00m, 15.00m, 200_000),
        ["claude-haiku-4-5-20251101"] = new(1.00m, 5.00m, 200_000),
        ["claude-haiku-4.5"] = new(1.00m, 5.00m, 200_000),

        // Claude 4.1
        ["claude-opus-4-1-20250501"] = new(15.00m, 75.00m, 200_000),
        ["claude-opus-4.1"] = new(15.00m, 75.00m, 200_000),

        // Claude 4.0
        ["claude-opus-4-20250514"] = new(15.00m, 75.00m, 200_000),
        ["claude-opus-4"] = new(15.00m, 75.00m, 200_000),
        ["claude-sonnet-4-20250514"] = new(3.00m, 15.00m, 200_000),
        ["claude-sonnet-4"] = new(3.00m, 15.00m, 200_000),

        // Claude 3.5
        ["claude-3-5-sonnet-20241022"] = new(3.00m, 15.00m, 200_000),
        ["claude-3.5-sonnet"] = new(3.00m, 15.00m, 200_000),
        ["claude-3-5-haiku-20241022"] = new(0.80m, 4.00m, 200_000),
        ["claude-haiku-3.5"] = new(0.80m, 4.00m, 200_000),

        // Claude 3.0
        ["claude-3-opus-20240229"] = new(15.00m, 75.00m, 200_000),
        ["claude-3-sonnet-20240229"] = new(3.00m, 15.00m, 200_000),
        ["claude-3-haiku-20240307"] = new(0.25m, 1.25m, 200_000),
        ["claude-3-haiku"] = new(0.25m, 1.25m, 200_000),
    };

    /// <summary>Anthropic Claude model pricing.</summary>
    public static IReadOnlyDictionary<string, ModelPricing> Anthropic => s_anthropic;

    // ═══════════════════════════════════════════════════════════════════════
    // Google Gemini
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, ModelPricing> s_google = new(StringComparer.OrdinalIgnoreCase)
    {
        // Gemini 3
        ["gemini-3-pro-preview"] = new(2.00m, 12.00m, 1_000_000),
        ["gemini-3-flash-preview"] = new(0.50m, 3.00m, 1_000_000),

        // Gemini 2.5
        ["gemini-2.5-pro"] = new(1.25m, 10.00m, 1_000_000),
        ["gemini-2.5-pro-preview-0514"] = new(1.25m, 10.00m, 1_000_000),
        ["gemini-2.5-flash"] = new(0.30m, 2.50m, 1_000_000),
        ["gemini-2.5-flash-preview-0514"] = new(0.30m, 2.50m, 1_000_000),
        ["gemini-2.5-flash-lite"] = new(0.10m, 0.40m, 1_000_000),

        // Gemini 2.0
        ["gemini-2.0-flash"] = new(0.10m, 0.40m, 1_000_000),
        ["gemini-2.0-flash-exp"] = new(0.10m, 0.40m, 1_000_000),
        ["gemini-2.0-flash-lite"] = new(0.075m, 0.30m, 1_000_000),

        // Gemini 1.5 (legacy)
        ["gemini-1.5-pro"] = new(1.25m, 5.00m, 1_000_000),
        ["gemini-1.5-flash"] = new(0.075m, 0.30m, 1_000_000),
    };

    /// <summary>Google Gemini model pricing.</summary>
    public static IReadOnlyDictionary<string, ModelPricing> Google => s_google;

    // ═══════════════════════════════════════════════════════════════════════
    // Mistral
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, ModelPricing> s_mistral = new(StringComparer.OrdinalIgnoreCase)
    {
        // Large
        ["mistral-large-3"] = new(0.50m, 1.50m, 131_072),
        ["mistral-large-2512"] = new(0.50m, 1.50m, 131_072),
        ["mistral-large-2411"] = new(2.00m, 6.00m, 131_072),
        ["mistral-large-latest"] = new(0.50m, 1.50m, 131_072),

        // Medium
        ["mistral-medium-3"] = new(0.40m, 2.00m, 131_072),
        ["mistral-medium-3.1"] = new(0.40m, 2.00m, 131_072),
        ["mistral-medium-latest"] = new(0.40m, 2.00m, 131_072),

        // Small
        ["mistral-small-3.1"] = new(0.03m, 0.11m, 32_768),
        ["mistral-small-3.2"] = new(0.06m, 0.18m, 32_768),
        ["mistral-small-latest"] = new(0.06m, 0.18m, 32_768),

        // Codestral
        ["codestral"] = new(0.30m, 0.90m, 256_000),
        ["codestral-2508"] = new(0.30m, 0.90m, 256_000),
        ["codestral-latest"] = new(0.30m, 0.90m, 256_000),

        // Devstral
        ["devstral-small"] = new(0.06m, 0.12m, 128_000),
        ["devstral-small-2505"] = new(0.06m, 0.12m, 128_000),
        ["devstral-medium"] = new(0.40m, 2.00m, 128_000),

        // Nemo
        ["mistral-nemo"] = new(0.02m, 0.07m, 128_000),
        ["open-mistral-nemo"] = new(0.02m, 0.07m, 128_000),

        // Pixtral
        ["pixtral-large"] = new(2.00m, 6.00m, 128_000),
        ["pixtral-12b"] = new(0.15m, 0.15m, 128_000),
    };

    /// <summary>Mistral AI model pricing.</summary>
    public static IReadOnlyDictionary<string, ModelPricing> Mistral => s_mistral;

    // ═══════════════════════════════════════════════════════════════════════
    // Ollama (local - free)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, ModelPricing> s_ollama = new(StringComparer.OrdinalIgnoreCase)
    {
        ["llama3.3"] = new(0m, 0m, 128_000),
        ["llama3.2"] = new(0m, 0m, 128_000),
        ["llama3.1"] = new(0m, 0m, 128_000),
        ["llama3"] = new(0m, 0m, 8_192),
        ["mistral"] = new(0m, 0m, 32_768),
        ["mixtral"] = new(0m, 0m, 32_768),
        ["codellama"] = new(0m, 0m, 16_384),
        ["deepseek-coder"] = new(0m, 0m, 16_384),
        ["phi3"] = new(0m, 0m, 128_000),
        ["qwen2.5"] = new(0m, 0m, 128_000),
    };

    /// <summary>Ollama local model pricing (free).</summary>
    public static IReadOnlyDictionary<string, ModelPricing> Ollama => s_ollama;

    // ═══════════════════════════════════════════════════════════════════════
    // Lookup Methods
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets pricing for a model by provider name.
    /// </summary>
    /// <param name="provider">Provider name (openai, anthropic, google, mistral, ollama).</param>
    /// <param name="model">Model name.</param>
    /// <returns>Pricing if found, null otherwise.</returns>
    public static ModelPricing? GetPricing(string? provider, string? model)
    {
        if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(model))
            return null;

        var table = provider switch
        {
            _ when provider.Equals("openai", StringComparison.OrdinalIgnoreCase) => s_openAi,
            _ when provider.Equals("azure", StringComparison.OrdinalIgnoreCase) ||
                   provider.Equals("azure_openai", StringComparison.OrdinalIgnoreCase) ||
                   provider.Equals("azure.ai.openai", StringComparison.OrdinalIgnoreCase) => s_openAi,
            _ when provider.Equals("anthropic", StringComparison.OrdinalIgnoreCase) => s_anthropic,
            _ when provider.Equals("google", StringComparison.OrdinalIgnoreCase) ||
                   provider.Equals("gemini", StringComparison.OrdinalIgnoreCase) ||
                   provider.Equals("gcp.gemini", StringComparison.OrdinalIgnoreCase) ||
                   provider.Equals("gcp.vertex_ai", StringComparison.OrdinalIgnoreCase) => s_google,
            _ when provider.Equals("mistral", StringComparison.OrdinalIgnoreCase) ||
                   provider.Equals("mistral_ai", StringComparison.OrdinalIgnoreCase) ||
                   provider.Equals("mistralai", StringComparison.OrdinalIgnoreCase) => s_mistral,
            _ when provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) => s_ollama,
            _ => null
        };

        return table?.GetValueOrDefault(model);
    }

    /// <summary>
    /// Calculates cost in USD for token usage.
    /// </summary>
    /// <param name="pricing">Model pricing.</param>
    /// <param name="inputTokens">Number of input tokens.</param>
    /// <param name="outputTokens">Number of output tokens.</param>
    /// <returns>Total cost in USD.</returns>
    public static decimal CalculateCost(ModelPricing pricing, long inputTokens, long outputTokens)
    {
        var inputCost = (inputTokens / 1_000_000m) * pricing.InputPerMTok;
        var outputCost = (outputTokens / 1_000_000m) * pricing.OutputPerMTok;
        return inputCost + outputCost;
    }

    /// <summary>
    /// Calculates cost in USD for token usage, with null-safe lookup.
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
    /// Calculates context utilization as a percentage.
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
    /// Gets the context window size for a model.
    /// </summary>
    /// <param name="provider">Provider name.</param>
    /// <param name="model">Model name.</param>
    /// <returns>Context window in tokens, or null if not found.</returns>
    public static int? GetContextWindow(string? provider, string? model)
    {
        return GetPricing(provider, model)?.ContextWindow;
    }
}
