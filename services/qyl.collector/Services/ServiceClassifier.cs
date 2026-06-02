namespace Qyl.Collector.Services;

public static class ServiceClassifier
{
    public const string TypeAiAgent = "ai_agent";
    public const string TypeMcpServer = "mcp_server";
    public const string TypeLlmProvider = "llm_provider";
    public const string TypeTraditional = "traditional";

    public static string Classify(
        IReadOnlyDictionary<string, string>? resourceAttributes,
        IReadOnlyDictionary<string, string>? spanAttributes)
    {
        if (IsClaudeCode(resourceAttributes, spanAttributes))
            return TypeAiAgent;

        if (HasAnyKey(
                spanAttributes,
                SemanticAttributeKeys.GenAiAgentName,
                SemanticAttributeKeys.GenAiAgentId,
                SemanticAttributeKeys.GenAiAgentDescription,
                SemanticAttributeKeys.GenAiAgentVersion))
            return TypeAiAgent;

        if (HasPrefixKey(spanAttributes, SemanticAttributeKeys.McpPrefix))
            return TypeMcpServer;

        if (HasKey(resourceAttributes, SemanticAttributeKeys.GenAiProviderName) ||
            HasKey(spanAttributes, SemanticAttributeKeys.GenAiProviderName))
            return TypeLlmProvider;

        return TypeTraditional;
    }

    private static bool IsClaudeCode(
        IReadOnlyDictionary<string, string>? resource,
        IReadOnlyDictionary<string, string>? span)
    {
        if (resource is not null &&
            resource.TryGetValue(SemanticAttributeKeys.OtelScopeName, out var scopeName) &&
            scopeName == "com.anthropic.claude_code")
            return true;

        return span is not null && span.Any(static kvp => kvp.Key.StartsWithOrdinal(SemanticAttributeKeys.ClaudeCodePrefix));
    }

    private static bool HasPrefixKey(
        IReadOnlyDictionary<string, string>? attrs,
        string prefix) =>
        attrs is not null && attrs.Any(kvp => kvp.Key.StartsWithOrdinal(prefix));

    private static bool HasKey(
        IReadOnlyDictionary<string, string>? attrs,
        string key) =>
        attrs is not null && attrs.ContainsKey(key);

    private static bool HasAnyKey(
        IReadOnlyDictionary<string, string>? attrs,
        params ReadOnlySpan<string> keys)
    {
        if (attrs is null) return false;
        foreach (var key in keys)
        {
            if (attrs.ContainsKey(key))
                return true;
        }

        return false;
    }
}
