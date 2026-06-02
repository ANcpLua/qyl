using ANcpLua.Agents;

namespace Qyl.Collector.Errors;

internal static class ErrorExtractor
{
    internal static ErrorEvent? Extract(SpanStorageRow span)
    {
        if (span.StatusCode != 2) return null;

        var attrs = ParseAttributesJson(span.AttributesJson);

        var exceptionType = attrs.GetValueOrDefault(SemanticAttributeKeys.ExceptionType)
                            ?? attrs.GetValueOrDefault(SemanticAttributeKeys.ErrorType)
                            ?? span.Name;
        var exceptionMessage = attrs.GetValueOrDefault(SemanticAttributeKeys.ExceptionMessage)
                               ?? span.StatusMessage
                               ?? "Unknown error";
        var stackTrace = attrs.GetValueOrDefault(SemanticAttributeKeys.ExceptionStacktrace);
        var genAiOperation = attrs.GetValueOrDefault(SemanticAttributeKeys.GenAiOperationName);
        var genAiFinishReasons = attrs.GetValueOrDefault(SemanticAttributeKeys.GenAiResponseFinishReasons);
        var genAiToolName = span.GenAiToolName ?? attrs.GetValueOrDefault(SemanticAttributeKeys.GenAiToolName);
        var genAiAgentName = attrs.GetValueOrDefault(SemanticAttributeKeys.GenAiAgentName);
        var genAiAgentId = attrs.GetValueOrDefault(SemanticAttributeKeys.GenAiAgentId);

        var category = ErrorCategorizer.Categorize(exceptionType, finishReason: genAiFinishReasons, message: exceptionMessage);
        var fingerprint = ErrorFingerprinter.Compute(
            exceptionType, exceptionMessage, stackTrace,
            genAiOperation,
            span.GenAiProviderName,
            span.GenAiRequestModel,
            genAiFinishReasons,
            category,
            span.ServiceName,
            span.Name);

        return new ErrorEvent
        {
            ErrorType = exceptionType,
            Message = exceptionMessage,
            Category = category,
            Fingerprint = fingerprint,
            ServiceName = span.ServiceName ?? "unknown",
            TraceId = span.TraceId,
            UserId = attrs.GetValueOrDefault(SemanticAttributeKeys.EnduserId)
                     ?? attrs.GetValueOrDefault(SemanticAttributeKeys.UserId),
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

    internal static Dictionary<string, string> ParseAttributesJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        return json.TryDeserialize(QylSerializerContext.Default.DictionaryStringString) ?? [];
    }
}
