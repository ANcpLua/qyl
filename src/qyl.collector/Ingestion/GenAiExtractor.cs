// qyl.collector - GenAI Field Extractor
// Extracts v1.38 semantic convention fields from span attributes

using System.Text.Json;

namespace qyl.collector.Ingestion;

/// <summary>
/// Extracts GenAI v1.38 semantic convention fields from JSON attributes.
/// </summary>
public static class GenAiExtractor
{
    // v1.38 GenAI semantic conventions
    private static class Attrs
    {
        public const string SystemName = "gen_ai.system";
        public const string RequestModel = "gen_ai.request.model";
        public const string ResponseModel = "gen_ai.response.model";
        public const string OperationName = "gen_ai.operation.name";

        public const string UsageInputTokens = "gen_ai.usage.input_tokens";
        public const string UsageOutputTokens = "gen_ai.usage.output_tokens";
        public const string UsageTotalTokens = "gen_ai.usage.total_tokens";

        // Legacy names for compatibility
        public const string LegacyPromptTokens = "gen_ai.response.prompt_tokens";
        public const string LegacyCompletionTokens = "gen_ai.response.completion_tokens";
    }

    /// <summary>
    /// Extracts typed GenAI fields from JSON attributes.
    /// </summary>
    public static GenAiFields Extract(string? attributesJson)
    {
        if (string.IsNullOrEmpty(attributesJson))
            return GenAiFields.Empty;

        try
        {
            using var doc = JsonDocument.Parse(attributesJson);
            var root = doc.RootElement;

            return new GenAiFields
            {
                System = GetString(root, Attrs.SystemName),
                RequestModel = GetString(root, Attrs.RequestModel),
                ResponseModel = GetString(root, Attrs.ResponseModel),
                OperationName = GetString(root, Attrs.OperationName),
                InputTokens = GetInt(root, Attrs.UsageInputTokens) ?? GetInt(root, Attrs.LegacyPromptTokens),
                OutputTokens = GetInt(root, Attrs.UsageOutputTokens) ?? GetInt(root, Attrs.LegacyCompletionTokens),
                TotalTokens = GetInt(root, Attrs.UsageTotalTokens)
            };
        }
        catch
        {
            return GenAiFields.Empty;
        }
    }

    /// <summary>
    /// Extracts GenAI fields from a JsonElement directly.
    /// </summary>
    public static GenAiFields Extract(JsonElement attributes)
    {
        return new GenAiFields
        {
            System = GetString(attributes, Attrs.SystemName),
            RequestModel = GetString(attributes, Attrs.RequestModel),
            ResponseModel = GetString(attributes, Attrs.ResponseModel),
            OperationName = GetString(attributes, Attrs.OperationName),
            InputTokens = GetInt(attributes, Attrs.UsageInputTokens) ?? GetInt(attributes, Attrs.LegacyPromptTokens),
            OutputTokens = GetInt(attributes, Attrs.UsageOutputTokens) ?? GetInt(attributes, Attrs.LegacyCompletionTokens),
            TotalTokens = GetInt(attributes, Attrs.UsageTotalTokens)
        };
    }

    /// <summary>
    /// Checks if the attributes contain GenAI fields.
    /// </summary>
    public static bool IsGenAiSpan(string? attributesJson)
    {
        if (string.IsNullOrEmpty(attributesJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(attributesJson);
            return doc.RootElement.TryGetProperty(Attrs.SystemName, out _) ||
                   doc.RootElement.TryGetProperty(Attrs.RequestModel, out _);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var i) ? i : null,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }
}

/// <summary>
/// Extracted GenAI fields.
/// </summary>
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
}
