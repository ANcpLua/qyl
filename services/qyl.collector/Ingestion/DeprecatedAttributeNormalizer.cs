namespace Qyl.Collector.Ingestion;

/// <summary>
///     Ingestion-time normalization of deprecated OTel attribute keys to their canonical
///     successors, per the contract in qyl-api-schema <c>VERSIONING.md</c> ("Ingestion mapping
///     (deprecated -&gt; current)"). Telemetry from older SDKs still carries the old keys; the
///     capture allow-lists are generated from the current semconv registry, so without this
///     rename the old keys would be silently dropped at ingest. Runs BEFORE the allow-list
///     check; when both the old and the canonical key are present, the canonical value wins.
///     Keys are renamed, values pass through untouched in their original AnyValue shape.
///     <para>
///     NOT legacy support — current-ecosystem support: as of 2026-07-11 the latest OpenAI .NET
///     SDK (2.12.0) still emits <c>gen_ai.system</c> with no <c>gen_ai.provider.name</c>, and
///     Microsoft.Extensions.AI 10.7.0 still carries the legacy spelling — both are
///     ActivitySources qyl subscribes to. The weekly <c>normalizer-expiry</c> workflow scans
///     the latest stable of those packages and opens the deletion issue the week they are all
///     clean; do not delete this type before that trigger fires.
///     </para>
/// </summary>
internal static class DeprecatedAttributeNormalizer
{
    /// <summary>
    ///     Returns the canonical key for <paramref name="key" /> and whether it was renamed.
    ///     A renamed key must not overwrite a value already present under the canonical key.
    /// </summary>
    internal static bool TryNormalize(string key, out string canonical)
    {
        // The gen_ai.*/agents.* left-hand keys were deleted from the registry and the shipped
        // assemblies, so literals are their only possible spelling; db.system still ships as a
        // deprecated constant, so it flows through the catalog like every known key.
        var mapped = key switch
        {
            "gen_ai.system" => CollectorSemanticAttributeCatalog.GenAiProviderName,
            "gen_ai.usage.prompt_tokens" => CollectorSemanticAttributeCatalog.GenAiInputTokens,
            "gen_ai.usage.completion_tokens" => CollectorSemanticAttributeCatalog.GenAiOutputTokens,
            "agents.tool.call_id" => CollectorSemanticAttributeCatalog.GenAiToolCallId,
            CollectorSemanticAttributeCatalog.DbSystemDeprecated => CollectorSemanticAttributeCatalog.DbSystemName,
            _ => null
        };

        canonical = mapped ?? key;
        return mapped is not null;
    }
}
