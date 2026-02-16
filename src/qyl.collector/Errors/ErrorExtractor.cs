namespace qyl.collector.Errors;

public static class ErrorExtractor
{
    public static ErrorEvent? Extract(SpanStorageRow span)
    {
        if (span.StatusCode != 2) return null;

        var attrs = ParseAttributesJson(span.AttributesJson);

        var exceptionType = attrs.GetValueOrDefault("exception.type")
                            ?? attrs.GetValueOrDefault("error.type")
                            ?? span.Name;
        var exceptionMessage = attrs.GetValueOrDefault("exception.message")
                               ?? span.StatusMessage
                               ?? "Unknown error";
        var stackTrace = attrs.GetValueOrDefault("exception.stacktrace");
        var genAiErrorType = attrs.GetValueOrDefault("gen_ai.error.type");
        var genAiOperation = attrs.GetValueOrDefault("gen_ai.operation.name");
        var genAiFinishReasons = attrs.GetValueOrDefault("gen_ai.response.finish_reasons");
        var genAiToolName = span.GenAiToolName ?? attrs.GetValueOrDefault("gen_ai.tool.name");
        var genAiAgentName = attrs.GetValueOrDefault("gen_ai.agent.name");
        var genAiAgentId = attrs.GetValueOrDefault("gen_ai.agent.id");

        var category = ErrorCategorizer.Categorize(exceptionType, genAiErrorType, genAiFinishReasons, exceptionMessage);
        var fingerprint = ErrorFingerprinter.Compute(
            exceptionType, exceptionMessage, stackTrace,
            genAiOperation,
            span.GenAiProviderName,
            span.GenAiRequestModel,
            genAiFinishReasons,
            category);

        return new ErrorEvent
        {
            ErrorType = exceptionType,
            Message = exceptionMessage,
            Category = category,
            Fingerprint = fingerprint,
            ServiceName = span.ServiceName ?? "unknown",
            TraceId = span.TraceId,
            UserId = attrs.GetValueOrDefault("enduser.id") ?? attrs.GetValueOrDefault("user.id"),
            GenAiProvider = span.GenAiProviderName,
            GenAiModel = span.GenAiRequestModel,
            GenAiOperation = genAiOperation,
            GenAiFinishReasons = genAiFinishReasons,
            GenAiToolName = genAiToolName,
            GenAiInputTokens = span.GenAiInputTokens,
            GenAiOutputTokens = span.GenAiOutputTokens,
            GenAiAgentName = genAiAgentName,
            GenAiAgentId = genAiAgentId
        };
    }

    /// <summary>
    ///     Parses a JSON attributes string to a string dictionary.
    ///     Shared utility for attribute deserialization across the collector.
    /// </summary>
    internal static Dictionary<string, string> ParseAttributesJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return JsonSerializer.Deserialize(json, QylSerializerContext.Default.DictionaryStringString) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
