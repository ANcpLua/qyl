namespace Qyl.Collector.Ingestion;

/// <summary>
///     Ingestion-time normalization of deprecated OTel attribute keys to their canonical
///     successors before the generated current-semconv allow-list runs. Current telemetry
///     producers still emit these spellings, so dropping them would lose supported data.
///     Canonical values win when both spellings are present; values otherwise pass through in
///     their original AnyValue shape.
/// </summary>
internal static class DeprecatedAttributeNormalizer
{
    /// <summary>
    ///     Returns the canonical key for <paramref name="key" /> and whether it was renamed.
    ///     A renamed key must not overwrite a value already present under the canonical key.
    /// </summary>
    internal static bool TryNormalize(string key, out string canonical)
    {
        // Removed registry keys remain literals; db.system still has a generated deprecated
        // constant. This list is the single owner of the supported ingest aliases.
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
