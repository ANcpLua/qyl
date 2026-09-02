namespace Qyl.Collector.Ingestion;

internal static class AttributeKeySets
{
    internal const string BaggagePrefix = CollectorSemanticAttributeCatalog.BaggagePrefix;

    internal static FrozenSet<string> SessionCorrelation => CollectorSemanticAttributeCatalog.SessionCorrelation;

    internal static FrozenSet<string> ProjectIdResourceKeys => CollectorSemanticAttributeCatalog.ProjectIdResourceKeys;

    // First-match lookups must read the precedence arrays, not the sets above: FrozenSet
    // enumeration order is an implementation detail, so a set-driven lookup silently picks a
    // winner by hash layout instead of by the order declared in collector-semantic-policy.json.
    internal static string[] SessionCorrelationPrecedence =>
        CollectorSemanticAttributeCatalog.SessionCorrelationPrecedence;

    internal static string[] ProjectIdResourceKeyPrecedence =>
        CollectorSemanticAttributeCatalog.ProjectIdResourceKeyPrecedence;

    internal static bool IsSafeSpanAttribute(string key) =>
        CollectorSemanticAttributeCatalog.SafeHttpSpanHeaderAttributeKeys.Contains(key) ||
        !IsDenied(key) && CollectorSemanticAttributeCatalog.SpanAttributeAllowList.Contains(key);

    internal static bool IsSafeLogAttribute(string key) =>
        !IsDenied(key) && CollectorSemanticAttributeCatalog.LogAttributeAllowList.Contains(key);

    // Metric attributes are the series identity, so an unregistered key would fork every
    // series it appears on and make the catalog unreadable. They pass exactly the same
    // registry-backed policy as span and log attributes.
    internal static bool IsSafeMetricAttribute(string key) =>
        !IsDenied(key) && CollectorSemanticAttributeCatalog.MetricAttributeAllowList.Contains(key);

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
        foreach (var prefix in CollectorSemanticAttributeCatalog.HttpHeaderAttributePrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // DeniedExactKeys only contains the keys these prefixes matched in the pinned semconv
        // packages. Entity-referenced resource attributes are application-named, so the prefixes
        // themselves have to be enforced here or unregistered PII keys pass the denial.
        foreach (var prefix in CollectorSemanticAttributeCatalog.DeniedKeyPrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (CollectorSemanticAttributeCatalog.DeniedExactKeys.Contains(key) ||
            key.StartsWith(BaggagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // The substring rule is deliberately blunt, so allowlisted keys that merely contain a
        // denied token (db.query.summary, gen_ai.token.type) opt out of it by exact name.
        if (CollectorSemanticAttributeCatalog.DeniedTokenExemptKeys.Contains(key))
            return false;

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
