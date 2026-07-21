namespace Qyl.Collector.Ingestion;

internal static class AttributeKeySets
{
    internal const string BaggagePrefix = CollectorSemanticAttributeCatalog.BaggagePrefix;

    internal static FrozenSet<string> SessionCorrelation => CollectorSemanticAttributeCatalog.SessionCorrelation;

    internal static FrozenSet<string> ProjectIdResourceKeys => CollectorSemanticAttributeCatalog.ProjectIdResourceKeys;

    internal static bool IsSafeSpanAttribute(string key) =>
        !IsDenied(key) && CollectorSemanticAttributeCatalog.SpanAttributeAllowList.Contains(key);

    internal static bool IsSafeLogAttribute(string key) =>
        !IsDenied(key) && CollectorSemanticAttributeCatalog.LogAttributeAllowList.Contains(key);

    internal static bool IsSafeResourceAttribute(string key) =>
        !IsDenied(key) &&
        (CollectorSemanticAttributeCatalog.ResourceAttributeAllowList.Contains(key) ||
         CollectorSemanticAttributeCatalog.QylResourceAttributeAllowList.Contains(key));

    // Entity references may identify resources with application-defined attributes that are not
    // part of the semantic-convention catalog. Persist only those explicitly referenced keys, and
    // keep the same credential/privacy boundary as every other persisted resource attribute.
    internal static bool IsSafeEntityReferencedResourceAttribute(string key) => !IsDenied(key);

    internal static bool ShouldCaptureSpanAttribute(string key) =>
        IsSafeSpanAttribute(key) ||
        SessionCorrelation.Contains(key) ||
        CollectorSemanticAttributeCatalog.SpanHotAttributeKeys.Contains(key);

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

}

internal static class AttributeLookupExtensions
{
    internal static string? GetFirstValueOrDefault(
        this IReadOnlyDictionary<string, OtlpAttributeValue> attributes,
        IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (attributes.GetValueOrDefault(key)?.AsString() is { } value)
                return value;
        }

        return null;
    }

    internal static bool IsAny(
        this string key,
        FrozenSet<string> candidates) =>
        candidates.Contains(key);
}
