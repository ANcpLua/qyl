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

        if (HasPrefixKey(spanAttributes, "gen_ai.agent."))
            return TypeAiAgent;

        if (HasPrefixKey(spanAttributes, "mcp."))
            return TypeMcpServer;

        if (HasKey(resourceAttributes, "gen_ai.provider.name") ||
            HasKey(spanAttributes, "gen_ai.provider.name"))
            return TypeLlmProvider;

        return TypeTraditional;
    }

    private static bool IsClaudeCode(
        IReadOnlyDictionary<string, string>? resource,
        IReadOnlyDictionary<string, string>? span)
    {
        if (resource is not null &&
            resource.TryGetValue("meter.name", out var meterName) &&
            meterName == "com.anthropic.claude_code")
            return true;

        return span is not null && span.Any(static kvp => kvp.Key.StartsWithOrdinal("claude_code."));
    }

    private static bool HasPrefixKey(
        IReadOnlyDictionary<string, string>? attrs,
        string prefix) =>
        attrs is not null && attrs.Any(kvp => kvp.Key.StartsWithOrdinal(prefix));

    private static bool HasKey(
        IReadOnlyDictionary<string, string>? attrs,
        string key) =>
        attrs is not null && attrs.ContainsKey(key);
}
