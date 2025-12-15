// =============================================================================
// qyl GenAI Attribute Extractor - SINGLE SOURCE OF TRUTH
// Extracts GenAI semantic convention attributes from span attributes
// Supports both dictionary and JSON-based attribute access
// =============================================================================

using System.Text.Json;

namespace qyl.collector;

/// <summary>
///     Extracts GenAI-specific attributes from span attribute collections.
///     Handles both current (OTel 1.38) and deprecated attribute names.
/// </summary>
public static class GenAiExtractor
{
    // =========================================================================
    // Dictionary-based extraction (IReadOnlyDictionary<string, object?>)
    // =========================================================================

    /// <summary>
    ///     Extracts GenAI fields from a dictionary of attributes.
    /// </summary>
    public static GenAiFields Extract(IReadOnlyDictionary<string, object?> attributes)
    {
        Throw.IfNull(attributes);

        var provider = GetString(attributes, GenAiAttributes.ProviderName)
                       ?? GetString(attributes, DeprecatedAttrs.System);

        var inputTokens = GetLong(attributes, GenAiAttributes.UsageInputTokens)
                          ?? GetLong(attributes, DeprecatedAttrs.UsagePromptTokens);

        var outputTokens = GetLong(attributes, GenAiAttributes.UsageOutputTokens)
                           ?? GetLong(attributes, DeprecatedAttrs.UsageCompletionTokens);

        return new GenAiFields
        {
            ProviderName = provider,
            OperationName = GetString(attributes, GenAiAttributes.OperationName),
            RequestModel = GetString(attributes, GenAiAttributes.RequestModel),
            ResponseModel = GetString(attributes, GenAiAttributes.ResponseModel),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = GetLong(attributes, GenAiAttributes.UsageTotalTokens),
            Temperature = GetDouble(attributes, GenAiAttributes.RequestTemperature),
            MaxTokens = GetLong(attributes, GenAiAttributes.RequestMaxTokens),
            FinishReason = GetString(attributes, GenAiAttributes.ResponseFinishReasons),
            CostUsd = GetDecimal(attributes, QylAttributes.CostUsd),
            SessionId = GetString(attributes, QylAttributes.SessionId)
                        ?? GetString(attributes, GenAiAttributes.ConversationId),
            ToolName = GetString(attributes, GenAiAttributes.ToolName),
            ToolCallId = GetString(attributes, GenAiAttributes.ToolCallId)
        };
    }

    /// <summary>
    ///     Checks if the attributes contain any GenAI-related keys.
    /// </summary>
    public static bool IsGenAiSpan(IReadOnlyDictionary<string, object?> attributes)
    {
        Throw.IfNull(attributes);

        return attributes.ContainsKey(GenAiAttributes.ProviderName) ||
               attributes.ContainsKey(DeprecatedAttrs.System) ||
               attributes.ContainsKey(GenAiAttributes.RequestModel);
    }

    /// <summary>
    ///     Checks if the attributes use deprecated GenAI attribute names.
    /// </summary>
    public static bool UsesDeprecatedAttributes(IReadOnlyDictionary<string, object?> attributes)
    {
        Throw.IfNull(attributes);

        return attributes.ContainsKey(DeprecatedAttrs.System) ||
               attributes.ContainsKey(DeprecatedAttrs.UsagePromptTokens) ||
               attributes.ContainsKey(DeprecatedAttrs.UsageCompletionTokens);
    }

    // =========================================================================
    // JSON-based extraction (string or JsonElement)
    // =========================================================================

    /// <summary>
    ///     Extracts GenAI fields from a JSON string of attributes.
    /// </summary>
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

    /// <summary>
    ///     Extracts GenAI fields from a JsonElement.
    /// </summary>
    public static GenAiFields Extract(JsonElement attributes)
    {
        var provider = GetJsonString(attributes, GenAiAttributes.ProviderName)
                       ?? GetJsonString(attributes, DeprecatedAttrs.System);

        var inputTokens = GetJsonLong(attributes, GenAiAttributes.UsageInputTokens)
                          ?? GetJsonLong(attributes, DeprecatedAttrs.UsagePromptTokens);

        var outputTokens = GetJsonLong(attributes, GenAiAttributes.UsageOutputTokens)
                           ?? GetJsonLong(attributes, DeprecatedAttrs.UsageCompletionTokens);

        return new GenAiFields
        {
            ProviderName = provider,
            OperationName = GetJsonString(attributes, GenAiAttributes.OperationName),
            RequestModel = GetJsonString(attributes, GenAiAttributes.RequestModel),
            ResponseModel = GetJsonString(attributes, GenAiAttributes.ResponseModel),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = GetJsonLong(attributes, GenAiAttributes.UsageTotalTokens),
            Temperature = GetJsonDouble(attributes, GenAiAttributes.RequestTemperature),
            MaxTokens = GetJsonLong(attributes, GenAiAttributes.RequestMaxTokens),
            FinishReason = GetJsonString(attributes, GenAiAttributes.ResponseFinishReasons),
            CostUsd = GetJsonDecimal(attributes, QylAttributes.CostUsd),
            SessionId = GetJsonString(attributes, QylAttributes.SessionId)
                        ?? GetJsonString(attributes, GenAiAttributes.ConversationId),
            ToolName = GetJsonString(attributes, GenAiAttributes.ToolName),
            ToolCallId = GetJsonString(attributes, GenAiAttributes.ToolCallId)
        };
    }

    /// <summary>
    ///     Checks if JSON attributes contain any GenAI-related keys.
    /// </summary>
    public static bool IsGenAiSpan(string? attributesJson)
    {
        if (string.IsNullOrEmpty(attributesJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(attributesJson);
            return IsGenAiSpan(doc.RootElement);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Checks if JsonElement contains any GenAI-related keys.
    /// </summary>
    public static bool IsGenAiSpan(JsonElement attributes) =>
        attributes.TryGetProperty(GenAiAttributes.ProviderName, out _) ||
        attributes.TryGetProperty(DeprecatedAttrs.System, out _) ||
        attributes.TryGetProperty(GenAiAttributes.RequestModel, out _);

    /// <summary>
    ///     Checks if JSON attributes use deprecated GenAI attribute names.
    /// </summary>
    public static bool UsesDeprecatedAttributes(string? attributesJson)
    {
        if (string.IsNullOrEmpty(attributesJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(attributesJson);
            return UsesDeprecatedAttributes(doc.RootElement);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Checks if JsonElement uses deprecated GenAI attribute names.
    /// </summary>
    public static bool UsesDeprecatedAttributes(JsonElement attributes) =>
        attributes.TryGetProperty(DeprecatedAttrs.System, out _) ||
        attributes.TryGetProperty(DeprecatedAttrs.UsagePromptTokens, out _) ||
        attributes.TryGetProperty(DeprecatedAttrs.UsageCompletionTokens, out _);

    // =========================================================================
    // Dictionary helpers
    // =========================================================================

    private static string? GetString(IReadOnlyDictionary<string, object?> attrs, string key) =>
        attrs.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static long? GetLong(IReadOnlyDictionary<string, object?> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var value) || value is null)
            return null;

        return value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            string s when long.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private static double? GetDouble(IReadOnlyDictionary<string, object?> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var value) || value is null)
            return null;

        return value switch
        {
            double d => d,
            float f => f,
            long l => l,
            int i => i,
            string s when double.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private static decimal? GetDecimal(IReadOnlyDictionary<string, object?> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var value) || value is null)
            return null;

        return value switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            float f => (decimal)f,
            long l => l,
            int i => i,
            string s when decimal.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    // =========================================================================
    // JSON helpers
    // =========================================================================

    private static string? GetJsonString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? GetJsonLong(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt64(out var l) ? l : null,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static double? GetJsonDouble(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDouble(out var d) ? d : null,
            JsonValueKind.String when double.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static decimal? GetJsonDecimal(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDecimal(out var d) ? d : null,
            JsonValueKind.String when decimal.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    // =========================================================================
    // Deprecated attribute names (OTel pre-1.38)
    // =========================================================================

    private static class DeprecatedAttrs
    {
        public const string System = "gen_ai.system";
        public const string UsagePromptTokens = "gen_ai.usage.prompt_tokens";
        public const string UsageCompletionTokens = "gen_ai.usage.completion_tokens";
    }
}

/// <summary>
///     Extracted GenAI span data.
/// </summary>
public sealed record GenAiFields
{
    public static readonly GenAiFields Empty = new();

    /// <summary>Provider name (e.g., "anthropic", "openai", "google").</summary>
    public string? ProviderName { get; init; }

    /// <summary>Operation name (e.g., "chat", "completion", "embedding").</summary>
    public string? OperationName { get; init; }

    /// <summary>Model ID from the request.</summary>
    public string? RequestModel { get; init; }

    /// <summary>Model ID from the response (may differ from request).</summary>
    public string? ResponseModel { get; init; }

    /// <summary>Number of input/prompt tokens.</summary>
    public long? InputTokens { get; init; }

    /// <summary>Number of output/completion tokens.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>Total tokens (if provided explicitly).</summary>
    public long? TotalTokens { get; init; }

    /// <summary>Request temperature parameter.</summary>
    public double? Temperature { get; init; }

    /// <summary>Request max tokens parameter.</summary>
    public long? MaxTokens { get; init; }

    /// <summary>Response finish/stop reason.</summary>
    public string? FinishReason { get; init; }

    /// <summary>Estimated cost in USD.</summary>
    public decimal? CostUsd { get; init; }

    /// <summary>Session/conversation ID.</summary>
    public string? SessionId { get; init; }

    /// <summary>Tool name (for tool calls).</summary>
    public string? ToolName { get; init; }

    /// <summary>Tool call ID (for tool calls).</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Effective model (response model if available, else request model).</summary>
    public string? Model => ResponseModel ?? RequestModel;

    /// <summary>Whether this span has token usage data.</summary>
    public bool HasTokenUsage => InputTokens.HasValue || OutputTokens.HasValue;

    /// <summary>Whether this represents a GenAI span.</summary>
    public bool IsGenAi => ProviderName is not null || Model is not null;

    /// <summary>Whether this is a tool call span.</summary>
    public bool IsToolCall => ToolName is not null || ToolCallId is not null;

    /// <summary>Computed total tokens (sum of input + output if total not provided).</summary>
    public long ComputedTotalTokens => TotalTokens ?? (InputTokens ?? 0) + (OutputTokens ?? 0);
}
