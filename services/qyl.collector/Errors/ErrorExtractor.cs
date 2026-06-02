using ANcpLua.Agents;
using EnduserAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Enduser.EnduserAttributes;
using ErrorAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Error.ErrorAttributes;
using ExceptionAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Exception.ExceptionAttributes;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;
using UserAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.User.UserAttributes;

namespace Qyl.Collector.Errors;

internal static class ErrorExtractor
{
    internal static ErrorEvent? Extract(SpanStorageRow span)
    {
        if (span.StatusCode != 2) return null;

        var attrs = ParseAttributesJson(span.AttributesJson);

        var exceptionType = attrs.GetValueOrDefault(ExceptionAttributes.Type)
                            ?? attrs.GetValueOrDefault(ErrorAttributes.Type)
                            ?? span.Name;
        var exceptionMessage = attrs.GetValueOrDefault(ExceptionAttributes.Message)
                               ?? span.StatusMessage
                               ?? "Unknown error";
        var stackTrace = attrs.GetValueOrDefault(ExceptionAttributes.Stacktrace);
        var genAiOperation = attrs.GetValueOrDefault(GenAiAttributes.OperationName);
        var genAiFinishReasons = attrs.GetValueOrDefault(GenAiAttributes.ResponseFinishReasons);
        var genAiToolName = span.GenAiToolName ?? attrs.GetValueOrDefault(GenAiAttributes.ToolName);
        var genAiAgentName = attrs.GetValueOrDefault(GenAiAttributes.AgentName);
        var genAiAgentId = attrs.GetValueOrDefault(GenAiAttributes.AgentId);

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
            UserId = attrs.GetValueOrDefault(EnduserAttributes.Id)
                     ?? attrs.GetValueOrDefault(UserAttributes.Id),
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
