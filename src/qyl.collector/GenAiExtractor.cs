namespace qyl.collector;

/// <summary>
///     Extracts GenAI-specific attributes from span attribute collections.
///     Handles both current (OTel 1.39) and deprecated attribute names.
/// </summary>
public static class GenAiExtractor
{
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

    // =========================================================================
    // Dictionary-based extraction (IReadOnlyDictionary<string, object?>)
    // =========================================================================

    /// <summary>
    ///     Extracts GenAI fields from a dictionary of attributes.
    /// </summary>
    public static GenAiFields Extract(IReadOnlyDictionary<string, object?> attributes)
    {
        Throw.IfNull(attributes);

        var inputTokens = GetLong(attributes, GenAiAttributes.UsageInputTokens);
        var outputTokens = GetLong(attributes, GenAiAttributes.UsageOutputTokens);

        return new GenAiFields
        {
            ProviderName = GetString(attributes, GenAiAttributes.ProviderName),
            OperationName = GetString(attributes, GenAiAttributes.OperationName),
            RequestModel = GetString(attributes, GenAiAttributes.RequestModel),
            ResponseModel = GetString(attributes, GenAiAttributes.ResponseModel),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = (inputTokens ?? 0) + (outputTokens ?? 0),
            Temperature = GetDouble(attributes, GenAiAttributes.RequestTemperature),
            MaxTokens = GetLong(attributes, GenAiAttributes.RequestMaxTokens),
            FinishReason = GetString(attributes, GenAiAttributes.ResponseFinishReasons),
            CostUsd = GetDouble(attributes, QylAttributes.CostUsd),
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
               attributes.ContainsKey(GenAiAttributes.RequestModel);
    }

    /// <summary>
    ///     Extracts GenAI fields from a JsonElement.
    /// </summary>
    public static GenAiFields Extract(JsonElement attributes)
    {
        var inputTokens = GetJsonLong(attributes, GenAiAttributes.UsageInputTokens);
        var outputTokens = GetJsonLong(attributes, GenAiAttributes.UsageOutputTokens);

        return new GenAiFields
        {
            ProviderName = GetJsonString(attributes, GenAiAttributes.ProviderName),
            OperationName = GetJsonString(attributes, GenAiAttributes.OperationName),
            RequestModel = GetJsonString(attributes, GenAiAttributes.RequestModel),
            ResponseModel = GetJsonString(attributes, GenAiAttributes.ResponseModel),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = (inputTokens ?? 0) + (outputTokens ?? 0),
            Temperature = GetJsonDouble(attributes, GenAiAttributes.RequestTemperature),
            MaxTokens = GetJsonLong(attributes, GenAiAttributes.RequestMaxTokens),
            FinishReason = GetJsonString(attributes, GenAiAttributes.ResponseFinishReasons),
            CostUsd = GetJsonDouble(attributes, QylAttributes.CostUsd),
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
        attributes.TryGetProperty(GenAiAttributes.RequestModel, out _);
}