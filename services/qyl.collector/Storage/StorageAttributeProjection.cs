namespace Qyl.Collector.Storage;

internal static class StorageAttributeProjection
{
    internal static SpanHotAttributeProjection ExtractSpanHotAttributes(
        IReadOnlyDictionary<string, string> attributes) =>
        new(
            SessionId: attributes.GetFirstValueOrDefault(AttributeKeySets.SessionCorrelation),
            GenAiProviderName: attributes.GetValueOrDefault(CollectorSemanticAttributeCatalog.GenAiProviderName),
            GenAiRequestModel: attributes.GetValueOrDefault(CollectorSemanticAttributeCatalog.GenAiRequestModel),
            GenAiResponseModel: attributes.GetValueOrDefault(CollectorSemanticAttributeCatalog.GenAiResponseModel),
            GenAiInputTokens: AttributeParsing.ParseNullableLong(
                attributes.GetValueOrDefault(CollectorSemanticAttributeCatalog.GenAiInputTokens)),
            GenAiOutputTokens: AttributeParsing.ParseNullableLong(
                attributes.GetValueOrDefault(CollectorSemanticAttributeCatalog.GenAiOutputTokens)),
            GenAiTemperature: ParseNullableDouble(
                attributes.GetValueOrDefault(CollectorSemanticAttributeCatalog.GenAiTemperature)),
            GenAiStopReason: attributes.GetValueOrDefault(CollectorSemanticAttributeCatalog.GenAiStopReason),
            GenAiToolName: attributes.GetValueOrDefault(CollectorSemanticAttributeCatalog.GenAiToolName),
            GenAiCostUsd: ParseNullableDouble(
                attributes.GetOptionalValueOrDefault(CollectorSemanticAttributeCatalog.GenAiCostUsd)));

    private static double? ParseNullableDouble(string? value) =>
        string.IsNullOrEmpty(value)
            ? null
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
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
