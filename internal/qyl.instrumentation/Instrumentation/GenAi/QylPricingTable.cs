using System.Collections.Frozen;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

public static class QylPricingTable
{
    private const string EmbeddedResourceName = "Qyl.Instrumentation.pricing.models.json";

    private static readonly Lazy<FrozenDictionary<string, PricingEntry>> s_table =
        new(LoadFromEmbeddedResource, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool TryGet(string? provider, string? model, out PricingEntry entry)
    {
        var table = s_table.Value;

        if (!string.IsNullOrEmpty(provider) && !string.IsNullOrEmpty(model)
            && table.TryGetValue(MakeKey(provider, model), out entry))
            return true;

        if (!string.IsNullOrEmpty(model) && table.TryGetValue(model!, out entry))
            return true;

        if (!string.IsNullOrEmpty(provider) && table.TryGetValue(MakeKey("*", provider!), out entry))
            return true;

        entry = default;
        return false;
    }

    public static int Count => s_table.Value.Count;

    private static string MakeKey(string provider, string model) => $"{provider}::{model}";

    private static FrozenDictionary<string, PricingEntry> LoadFromEmbeddedResource()
    {
        var assembly = typeof(QylPricingTable).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
        if (stream is null)
            return FrozenDictionary<string, PricingEntry>.Empty;

        using var doc = JsonDocument.Parse(stream);
        var entries = new Dictionary<string, PricingEntry>(StringComparer.Ordinal);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name.StartsWith('_') || prop.Value.ValueKind != JsonValueKind.Object)
                continue;

            if (!TryParse(prop.Value, out var entry))
                continue;

            if (prop.Name.EndsWith("/*"))
            {
                var providerOnly = prop.Name[..^2];
                entries[MakeKey("*", providerOnly)] = entry;
                continue;
            }

            entries[prop.Name] = entry;

            if (!string.IsNullOrEmpty(entry.Provider))
                entries[MakeKey(entry.Provider, prop.Name)] = entry;
        }

        return entries.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static bool TryParse(JsonElement el, out PricingEntry entry)
    {
        var input = TryGetDouble(el, "input_cost_per_token");
        var output = TryGetDouble(el, "output_cost_per_token");

        if (input is null && output is null)
        {
            entry = default;
            return false;
        }

        var provider = el.TryGetProperty("litellm_provider", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? string.Empty
            : string.Empty;

        entry = new PricingEntry(
            InputCostPerToken: input ?? 0d,
            OutputCostPerToken: output ?? 0d,
            CacheReadInputTokenCost: TryGetDouble(el, "cache_read_input_token_cost"),
            CacheCreationInputTokenCost: TryGetDouble(el, "cache_creation_input_token_cost"),
            OutputCostPerReasoningToken: TryGetDouble(el, "output_cost_per_reasoning_token"),
            Provider: provider);
        return true;
    }

    private static double? TryGetDouble(JsonElement el, string property) =>
        el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : null;
}

public readonly record struct PricingEntry(
    double InputCostPerToken,
    double OutputCostPerToken,
    double? CacheReadInputTokenCost,
    double? CacheCreationInputTokenCost,
    double? OutputCostPerReasoningToken,
    string Provider);
