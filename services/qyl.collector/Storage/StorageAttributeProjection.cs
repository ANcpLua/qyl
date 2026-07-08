namespace Qyl.Collector.Storage;

internal static class StorageAttributeProjection
{
    internal static SpanHotAttributeProjection ExtractSpanHotAttributes(
        IReadOnlyDictionary<string, OtlpAttributeValue> attributes) =>
        new(
            SessionId: attributes.GetFirstValueOrDefault(AttributeKeySets.SessionCorrelation),
            GenAiProviderName: attributes.GetString(CollectorSemanticAttributeCatalog.GenAiProviderName),
            GenAiRequestModel: attributes.GetString(CollectorSemanticAttributeCatalog.GenAiRequestModel),
            GenAiResponseModel: attributes.GetString(CollectorSemanticAttributeCatalog.GenAiResponseModel),
            GenAiInputTokens: attributes.GetInt64(CollectorSemanticAttributeCatalog.GenAiInputTokens),
            GenAiOutputTokens: attributes.GetInt64(CollectorSemanticAttributeCatalog.GenAiOutputTokens),
            GenAiCacheReadInputTokens: attributes.GetInt64(CollectorSemanticAttributeCatalog.GenAiCacheReadInputTokens),
            GenAiCacheCreationInputTokens: attributes.GetInt64(CollectorSemanticAttributeCatalog.GenAiCacheCreationInputTokens),
            GenAiReasoningTokens: attributes.GetInt64(CollectorSemanticAttributeCatalog.GenAiReasoningTokens),
            GenAiTemperature: attributes.GetDouble(CollectorSemanticAttributeCatalog.GenAiTemperature),
            GenAiStopReason: attributes.GetString(CollectorSemanticAttributeCatalog.GenAiStopReason),
            GenAiToolName: attributes.GetString(CollectorSemanticAttributeCatalog.GenAiToolName));

    private static string? GetString(this IReadOnlyDictionary<string, OtlpAttributeValue> attributes, string? key) =>
        key is null ? null : attributes.GetValueOrDefault(key)?.AsString();

    private static long? GetInt64(this IReadOnlyDictionary<string, OtlpAttributeValue> attributes, string? key) =>
        key is null ? null : attributes.GetValueOrDefault(key)?.AsInt64();

    private static double? GetDouble(this IReadOnlyDictionary<string, OtlpAttributeValue> attributes, string? key) =>
        key is null ? null : attributes.GetValueOrDefault(key)?.AsDouble();
}

internal readonly record struct SpanHotAttributeProjection(
    string? SessionId,
    string? GenAiProviderName,
    string? GenAiRequestModel,
    string? GenAiResponseModel,
    long? GenAiInputTokens,
    long? GenAiOutputTokens,
    long? GenAiCacheReadInputTokens,
    long? GenAiCacheCreationInputTokens,
    long? GenAiReasoningTokens,
    double? GenAiTemperature,
    string? GenAiStopReason,
    string? GenAiToolName);
