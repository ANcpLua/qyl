using System.Text.Json;
using qyl.agents.telemetry;

namespace qyl.collector.Ingestion;

public static class GenAiExtractor
{
    public static GenAiFields Extract(string? attributesJson)
    {
        if (string.IsNullOrEmpty(attributesJson))
            return GenAiFields.Empty;

        try
        {
            using var doc = JsonDocument.Parse(attributesJson);
            return Extract(doc.RootElement);
        }
        catch
        {
            return GenAiFields.Empty;
        }
    }

    public static GenAiFields Extract(JsonElement attributes)
    {
        string? provider = GetString(attributes, Attrs.ProviderName)
                           ?? GetString(attributes, Attrs.LegacySystem);

        int? inputTokens = GetInt(attributes, Attrs.UsageInputTokens)
                           ?? GetInt(attributes, Attrs.LegacyPromptTokens);

        int? outputTokens = GetInt(attributes, Attrs.UsageOutputTokens)
                            ?? GetInt(attributes, Attrs.LegacyCompletionTokens);

        return new GenAiFields
        {
            System = provider,
            RequestModel = GetString(attributes, Attrs.RequestModel),
            ResponseModel = GetString(attributes, Attrs.ResponseModel),
            OperationName = GetString(attributes, Attrs.OperationName),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = GetInt(attributes, Attrs.UsageTotalTokens)
        };
    }

    public static bool IsGenAiSpan(string? attributesJson)
    {
        if (string.IsNullOrEmpty(attributesJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(attributesJson);
            JsonElement root = doc.RootElement;

            return root.TryGetProperty(Attrs.ProviderName, out _) ||
                   root.TryGetProperty(Attrs.LegacySystem, out _) ||
                   root.TryGetProperty(Attrs.RequestModel, out _);
        }
        catch
        {
            return false;
        }
    }

    public static bool UsesDeprecatedAttributes(string? attributesJson)
    {
        if (string.IsNullOrEmpty(attributesJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(attributesJson);
            JsonElement root = doc.RootElement;

            return root.TryGetProperty(Attrs.LegacySystem, out _) ||
                   root.TryGetProperty(Attrs.LegacyPromptTokens, out _) ||
                   root.TryGetProperty(Attrs.LegacyCompletionTokens, out _);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out int i) ? i : null,
            JsonValueKind.String when int.TryParse(value.GetString(), out int parsed) => parsed,
            _ => null
        };
    }

    private static class Attrs
    {
        public const string ProviderName = GenAiAttributes.ProviderName;
        public const string RequestModel = GenAiAttributes.RequestModel;
        public const string ResponseModel = GenAiAttributes.ResponseModel;
        public const string OperationName = GenAiAttributes.OperationName;
        public const string UsageInputTokens = GenAiAttributes.UsageInputTokens;
        public const string UsageOutputTokens = GenAiAttributes.UsageOutputTokens;
        public const string UsageTotalTokens = GenAiAttributes.UsageTotalTokens;

        public const string LegacySystem = "gen_ai.system";
        public const string LegacyPromptTokens = "gen_ai.usage.prompt_tokens";
        public const string LegacyCompletionTokens = "gen_ai.usage.completion_tokens";
    }
}

public sealed record GenAiFields
{
    public static readonly GenAiFields Empty = new();

    public string? System { get; init; }

    public string? RequestModel { get; init; }

    public string? ResponseModel { get; init; }

    public string? OperationName { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public int? TotalTokens { get; init; }

    public string? Model => ResponseModel ?? RequestModel;

    public bool HasTokenUsage => InputTokens.HasValue || OutputTokens.HasValue;

    public bool IsGenAi => System is not null || Model is not null;

    public int ComputedTotalTokens => TotalTokens ?? (InputTokens ?? 0) + (OutputTokens ?? 0);
}
