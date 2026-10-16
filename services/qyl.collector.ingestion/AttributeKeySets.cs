namespace Qyl.Collector.Ingestion;

internal static class AttributeKeySets
{
    internal const string BaggagePrefix = CollectorSemanticAttributeCatalog.BaggagePrefix;

    internal static FrozenSet<string> SessionCorrelation => CollectorSemanticAttributeCatalog.SessionCorrelation;

    // First-match lookups must read the precedence arrays, not the sets above: FrozenSet
    // enumeration order is an implementation detail, so a set-driven lookup silently picks a
    // winner by hash layout instead of by the order declared in collector-semantic-policy.json.
    internal static string[] SessionCorrelationPrecedence =>
        CollectorSemanticAttributeCatalog.SessionCorrelationPrecedence;

    internal static string[] ProjectIdResourceKeyPrecedence =>
        CollectorSemanticAttributeCatalog.ProjectIdResourceKeyPrecedence;

    internal static bool IsSafeSpanAttribute(string key) =>
        IsPersistable(key) &&
        (CollectorSemanticAttributeCatalog.SpanAttributeAllowList.Contains(key) || IsVendorTag(key) || IsAppAttribute(key));

    internal static bool IsSafeLogAttribute(string key) =>
        IsPersistable(key) &&
        (CollectorSemanticAttributeCatalog.LogAttributeAllowList.Contains(key) || IsVendorTag(key));

    // Metric attributes are the series identity, so an unregistered key would fork every
    // series it appears on and make the catalog unreadable. They pass exactly the same
    // registry-backed policy as span and log attributes.
    internal static bool IsSafeMetricAttribute(string key) =>
        IsPersistable(key) &&
        (CollectorSemanticAttributeCatalog.MetricAttributeAllowList.Contains(key) || IsVendorTag(key));

    // A key a pinned third-party library emits is a tag, not vocabulary: it is forwarded exactly
    // as it arrived and never rewritten. The prefix and exact denials still apply to it — a
    // vendor is no more entitled to write a credential into storage than an application is — and
    // only the blunt token rule steps aside for it (see IsPersistable). The keys are RegistryPolicy.VendorKeys.
    private static bool IsVendorTag(string key) => CollectorSemanticAttributeCatalog.VendorKeys.Contains(key);

    /// <summary>
    /// A business attribute <c>qyl.app.&lt;key&gt;</c>, written by <c>SetAppTag</c>. Its keys are named by the
    /// application, so the namespace is captured by prefix; the per-span bounds apply at ingest.
    /// </summary>
    internal static bool IsAppAttribute(string key) =>
        key.Length > CollectorSemanticAttributeCatalog.AppAttributePrefix.Length &&
        key.StartsWith(CollectorSemanticAttributeCatalog.AppAttributePrefix, StringComparison.Ordinal);

    internal static bool IsSafeResourceAttribute(string key) =>
        IsPersistable(key) &&
        (CollectorSemanticAttributeCatalog.ResourceAttributeAllowList.Contains(key) ||
         CollectorSemanticAttributeCatalog.QylResourceAttributeAllowList.Contains(key));

    // Entity references may identify resources with application-defined attributes that are not
    // part of the semantic-convention catalog. Persist only those explicitly referenced keys, and
    // keep the same credential/privacy boundary as every other persisted resource attribute.
    // qyl.app.* is bounded per span, event and link; a resource has no such bound, so it never carries one.
    internal static bool IsSafeEntityReferencedResourceAttribute(string key) => IsPersistable(key) && !IsAppAttribute(key);

    internal static bool ShouldCaptureSpanAttribute(string key) =>
        IsSafeSpanAttribute(key) ||
        SessionCorrelation.Contains(key) ||
        CollectorSemanticAttributeCatalog.SpanHotAttributeKeys.Contains(key);

    /// <summary>
    /// Whether the registry lets the collector persist <paramref name="key"/> at all, in
    /// <c>RegistryPolicy.IsPersisted</c>'s order, plus two denials only a collector applies: the baggage prefix,
    /// and the registry's key and prefix denials applied again after <c>qyl.app.</c>.
    /// The allowlists above decide which persistable keys are kept.
    /// </summary>
    internal static bool IsPersistable(string key)
    {
        if (IsDeniedKeyOrPrefix(key))
            return false;

        // SetAppTag names the key after qyl.app., so qyl.app.user.email is as personal as user.email.
        if (key.StartsWith(CollectorSemanticAttributeCatalog.AppAttributePrefix, StringComparison.OrdinalIgnoreCase) &&
            IsDeniedKeyOrPrefix(key.AsSpan(CollectorSemanticAttributeCatalog.AppAttributePrefix.Length)))
            return false;

        if (key.StartsWith(BaggagePrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        // The token rule is deliberately blunt, so registry keys that merely contain a denied token
        // (db.query.summary, gen_ai.usage.input_tokens) opt out of it by exact name, and so does every
        // vendor pass-through key: the vendor spells its own tags, and a dozen of them
        // (messaging.masstransit.message_id, nservicebus.message_intent, execution.result) contain a
        // denied token while carrying nothing the token exists to catch. The exact and prefix denials
        // above are privacy rules and run ahead of this for a vendor key exactly as for any other key.
        if (CollectorSemanticAttributeCatalog.TokenExemptKeys.Contains(key) || IsVendorTag(key))
            return true;

        foreach (var token in CollectorSemanticAttributeCatalog.DeniedKeyTokens)
        {
            if (key.Contains(token, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    // Prefixes, not their registry expansion: an application-defined key under one of these
    // namespaces (user.email_address, enduser.tenant) is denied as surely as a registry key.
    private static bool IsDeniedKeyOrPrefix(ReadOnlySpan<char> key)
    {
        if (IsNotPersistedKeyOrTemplateChild(key))
            return true;

        foreach (var prefix in CollectorSemanticAttributeCatalog.NotPersistedPrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> s_notPersistedKeys =
        CollectorSemanticAttributeCatalog.NotPersistedKeys.GetAlternateLookup<ReadOnlySpan<char>>();

    // A denied template key denies its children too: http.request.header denies http.request.header.<name>.
    private static bool IsNotPersistedKeyOrTemplateChild(ReadOnlySpan<char> key)
    {
        var candidate = key;
        while (true)
        {
            if (s_notPersistedKeys.Contains(candidate))
                return true;

            var dot = candidate.LastIndexOf('.');
            if (dot <= 0)
                return false;

            candidate = candidate[..dot];
        }
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
