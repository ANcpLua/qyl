using System.Text.Json;
using qyl.collector.Storage;

namespace qyl.collector.Errors;

public static class ErrorExtractor
{
    public static ErrorEvent? Extract(SpanStorageRow span)
    {
        if (span.StatusCode != 2) return null;

        var attrs = ParseAttributes(span.AttributesJson);

        var exceptionType = attrs.GetValueOrDefault("exception.type")
                           ?? attrs.GetValueOrDefault("error.type")
                           ?? span.Name;
        var exceptionMessage = attrs.GetValueOrDefault("exception.message")
                              ?? span.StatusMessage
                              ?? "Unknown error";
        var stackTrace = attrs.GetValueOrDefault("exception.stacktrace");
        var genAiErrorType = attrs.GetValueOrDefault("gen_ai.error.type");
        var genAiOperation = attrs.GetValueOrDefault("gen_ai.operation.name");

        var category = ErrorCategorizer.Categorize(exceptionType, genAiErrorType);
        var fingerprint = ErrorFingerprinter.Compute(exceptionType, exceptionMessage, stackTrace, genAiOperation);

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
        };
    }

    private static Dictionary<string, string> ParseAttributes(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
