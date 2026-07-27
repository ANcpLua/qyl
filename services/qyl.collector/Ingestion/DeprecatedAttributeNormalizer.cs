namespace Qyl.Collector.Ingestion;

internal static class DeprecatedAttributeNormalizer
{
    internal static bool TryNormalize(string key, out string canonical)
    {
        var mapped = key switch
        {
            "gen_ai.system" => CollectorSemanticAttributeCatalog.GenAiProviderName,
            "gen_ai.usage.prompt_tokens" => CollectorSemanticAttributeCatalog.GenAiInputTokens,
            "gen_ai.usage.completion_tokens" => CollectorSemanticAttributeCatalog.GenAiOutputTokens,
            "agents.tool.call_id" => CollectorSemanticAttributeCatalog.GenAiToolCallId,
            CollectorSemanticAttributeCatalog.DbSystemDeprecated => CollectorSemanticAttributeCatalog.DbSystemName,
            // Both canonical spellings are in deniedExactKeys so raw SQL and full URLs never
            // reach storage. Normalizing here is what applies that denial to pre-1.21/pre-1.26
            // instrumentation, which would otherwise write credentials straight through.
            CollectorSemanticAttributeCatalog.DbStatementDeprecated => CollectorSemanticAttributeCatalog.DbQueryText,
            CollectorSemanticAttributeCatalog.HttpUrlDeprecated => CollectorSemanticAttributeCatalog.UrlFull,
            _ => null
        };

        canonical = mapped ?? key;
        return mapped is not null;
    }
}
