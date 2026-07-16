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
            _ => null
        };

        canonical = mapped ?? key;
        return mapped is not null;
    }
}
