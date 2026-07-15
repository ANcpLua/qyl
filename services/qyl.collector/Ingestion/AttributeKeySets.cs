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

    // Metric dimensions are checked against their own allow-list, and registry keys that merely
    // contain a denied credential token (e.g. gen_ai.token.type) skip the token scan while still
    // honoring the exact deny-list and baggage prefix.
    internal static bool IsSafeMetricAttribute(string key) =>
        !IsDeniedMetricKey(key) && CollectorSemanticAttributeCatalog.MetricAttributeAllowList.Contains(key);

    // Descriptor metadata, scope attributes, and exemplar filtered attributes are not metric
    // dimensions and therefore are not constrained to the semantic-convention dimension catalog.
    // They still cross the same persistence privacy boundary and honor the credential deny rules.
    internal static bool IsSafeMetricAuxiliaryAttribute(string key) => !IsDeniedMetricKey(key);

    private static bool IsDeniedMetricKey(string key) =>
        CollectorSemanticAttributeCatalog.DeniedTokenExemptKeys.Contains(key)
            ? CollectorSemanticAttributeCatalog.DeniedExactKeys.Contains(key) ||
              key.StartsWith(BaggagePrefix, StringComparison.OrdinalIgnoreCase)
            : IsDenied(key);

    internal static bool IsSafeProfileAttribute(string key) =>
        !IsDenied(key) && CollectorSemanticAttributeCatalog.ProfileAttributeAllowList.Contains(key);

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
