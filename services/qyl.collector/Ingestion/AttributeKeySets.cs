using System.Collections.Frozen;

namespace Qyl.Collector.Ingestion;

internal static class AttributeKeySets
{
    internal const string BaggagePrefix = CollectorSemanticAttributeCatalog.BaggagePrefix;

    internal static FrozenSet<string> SessionCorrelation => CollectorSemanticAttributeCatalog.SessionCorrelation;

    internal static FrozenSet<string> ProjectIdResourceKeys => CollectorSemanticAttributeCatalog.ProjectIdResourceKeys;

    internal static bool ShouldPersistSpanAttribute(string key) =>
        !IsDenied(key) && CollectorSemanticAttributeCatalog.SpanAttributeAllowList.Contains(key);

    internal static bool ShouldPersistLogAttribute(string key) =>
        !IsDenied(key) && CollectorSemanticAttributeCatalog.LogAttributeAllowList.Contains(key);

    internal static bool ShouldPersistProfileAttribute(string key) =>
        !IsDenied(key) && CollectorSemanticAttributeCatalog.ProfileAttributeAllowList.Contains(key);

    internal static bool ShouldPersistResourceAttribute(string key) =>
        !IsDenied(key) &&
        (CollectorSemanticAttributeCatalog.ResourceAttributeAllowList.Contains(key) ||
         CollectorSemanticAttributeCatalog.QylResourceAttributeAllowList.Contains(key));

    internal static bool ShouldConvertSpanAttribute(string key) =>
        ShouldPersistSpanAttribute(key) ||
        SessionCorrelation.Contains(key) ||
        CollectorSemanticAttributeCatalog.SpanStorageProjectionKeys.Contains(key);

    internal static SpanStorageProjection ExtractSpanStorageProjection(IReadOnlyDictionary<string, string> attributes) =>
        new(
            SessionId: attributes.GetFirstValueOrDefault(SessionCorrelation),
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
            GenAiCostUsd: ParseNullableDouble(attributes.GetValueOrDefault(CollectorSemanticAttributeCatalog.GenAiCostUsd)));

    private static bool IsDenied(string key)
    {
        if (CollectorSemanticAttributeCatalog.DeniedExactKeys.Contains(key) ||
            key.StartsWith(BaggagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var token in CollectorSemanticAttributeCatalog.DeniedKeyTokens)
        {
            if (key.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static double? ParseNullableDouble(string? value) =>
        string.IsNullOrEmpty(value)
            ? null
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
}

internal readonly record struct SpanStorageProjection(
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

internal static class AttributeLookupExtensions
{
    internal static string? GetFirstValueOrDefault(
        this IReadOnlyDictionary<string, string> attributes,
        IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (attributes.GetValueOrDefault(key) is { } value)
                return value;
        }

        return null;
    }

    internal static bool IsAny(
        this string key,
        FrozenSet<string> candidates) =>
        candidates.Contains(key);
}
