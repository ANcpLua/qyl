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
            GenAiTemperature: attributes.GetDouble(CollectorSemanticAttributeCatalog.GenAiTemperature),
            GenAiStopReason: attributes.GetString(CollectorSemanticAttributeCatalog.GenAiStopReason),
            GenAiToolName: attributes.GetString(CollectorSemanticAttributeCatalog.GenAiToolName),
            GenAiCostUsd: attributes.GetDouble(CollectorSemanticAttributeCatalog.GenAiCostUsd));

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
    double? GenAiTemperature,
    string? GenAiStopReason,
    string? GenAiToolName,
    double? GenAiCostUsd);
