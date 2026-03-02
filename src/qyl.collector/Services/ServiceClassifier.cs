namespace qyl.collector.Services;

/// <summary>
///     Classifies services by type using OTel attribute inspection.
///     Priority-ordered rules — first match wins.
/// </summary>
public static class ServiceClassifier
{
    public const string TypeAiAgent = "ai_agent";
    public const string TypeMcpServer = "mcp_server";
    public const string TypeLlmProvider = "llm_provider";
    public const string TypeTraditional = "traditional";

    /// <summary>
    ///     Classifies a service based on resource + span attributes.
    /// </summary>
    public static string Classify(
        IReadOnlyDictionary<string, string>? resourceAttributes,
        IReadOnlyDictionary<string, string>? spanAttributes)
    {
        // P1: Claude Code detection
        if (IsClaudeCode(resourceAttributes, spanAttributes))
            return TypeAiAgent;

        // P2: Generic AI agent (gen_ai.agent.* attributes)
        if (HasPrefixKey(spanAttributes, "gen_ai.agent."))
            return TypeAiAgent;

        // P3: MCP server (mcp.* attributes)
        if (HasPrefixKey(spanAttributes, "mcp."))
            return TypeMcpServer;

        // P4: LLM provider (gen_ai.provider.name present anywhere)
        if (HasKey(resourceAttributes, "gen_ai.provider.name") ||
            HasKey(spanAttributes, "gen_ai.provider.name"))
            return TypeLlmProvider;

        // P5: Default
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
        string prefix)
    {
        return attrs is not null && attrs.Any(kvp => kvp.Key.StartsWithOrdinal(prefix));
    }

    private static bool HasKey(
        IReadOnlyDictionary<string, string>? attrs,
        string key) =>
        attrs is not null && attrs.ContainsKey(key);
}
