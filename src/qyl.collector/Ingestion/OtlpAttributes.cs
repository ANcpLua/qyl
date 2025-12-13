// =============================================================================
// qyl OTLP Ingestion - Attribute Constants for Zero-Allocation Parsing
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.38.0
// =============================================================================

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace qyl.collector.Ingestion;

/// <summary>
///     OTel 1.38 GenAI attribute keys as UTF-8 byte spans for zero-allocation OTLP parsing.
///     Uses direct StartsWith checks for prefix matching (NOT SearchValues which is for substring).
/// </summary>
public static class OtlpGenAiAttributes
{
    // Frozen dictionary for O(1) deprecated → current mapping
    private static readonly FrozenDictionary<string, string> _deprecatedMap =
        new Dictionary<string, string>
        {
            ["gen_ai.system"] = "gen_ai.provider.name",
            ["gen_ai.usage.prompt_tokens"] = "gen_ai.usage.input_tokens",
            ["gen_ai.usage.completion_tokens"] = "gen_ai.usage.output_tokens"
        }.ToFrozenDictionary();

    // Required attributes (OTel 1.38)
    public static ReadOnlySpan<byte> ProviderName => "gen_ai.provider.name"u8;
    public static ReadOnlySpan<byte> RequestModel => "gen_ai.request.model"u8;
    public static ReadOnlySpan<byte> OperationName => "gen_ai.operation.name"u8;
    public static ReadOnlySpan<byte> InputTokens => "gen_ai.usage.input_tokens"u8;
    public static ReadOnlySpan<byte> OutputTokens => "gen_ai.usage.output_tokens"u8;

    // Recommended attributes
    public static ReadOnlySpan<byte> ResponseModel => "gen_ai.response.model"u8;
    public static ReadOnlySpan<byte> RequestTemperature => "gen_ai.request.temperature"u8;
    public static ReadOnlySpan<byte> RequestMaxTokens => "gen_ai.request.max_tokens"u8;
    public static ReadOnlySpan<byte> RequestTopP => "gen_ai.request.top_p"u8;
    public static ReadOnlySpan<byte> RequestStopSequences => "gen_ai.request.stop_sequences"u8;
    public static ReadOnlySpan<byte> ResponseFinishReasons => "gen_ai.response.finish_reasons"u8;
    public static ReadOnlySpan<byte> ResponseId => "gen_ai.response.id"u8;

    // Agent attributes (anthropic.*/agents.* registry)
    public static ReadOnlySpan<byte> AgentId => "agents.agent.id"u8;
    public static ReadOnlySpan<byte> AgentName => "agents.agent.name"u8;
    public static ReadOnlySpan<byte> AgentDescription => "agents.agent.description"u8;
    public static ReadOnlySpan<byte> ToolName => "agents.tool.name"u8;
    public static ReadOnlySpan<byte> ToolCallId => "agents.tool.call_id"u8;

    // Deprecated mappings (for backward compat parsing)
    public static ReadOnlySpan<byte> DeprecatedSystem => "gen_ai.system"u8;
    public static ReadOnlySpan<byte> DeprecatedPromptTokens => "gen_ai.usage.prompt_tokens"u8;
    public static ReadOnlySpan<byte> DeprecatedCompletionTokens => "gen_ai.usage.completion_tokens"u8;

    /// <summary>
    ///     Checks if the key starts with "gen_ai." prefix using direct comparison.
    ///     NOTE: Do NOT use SearchValues for prefix matching - it does substring matching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGenAiAttribute(ReadOnlySpan<byte> key)
    {
        if (key.Length < 7) return false;

        // Direct prefix check - NOT SearchValues which does substring matching
        return key[..7].SequenceEqual("gen_ai."u8);
    }

    /// <summary>
    ///     Checks if the key starts with "agents." prefix using direct comparison.
    ///     NOTE: Do NOT use SearchValues for prefix matching - it does substring matching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAgentsAttribute(ReadOnlySpan<byte> key)
    {
        if (key.Length < 7) return false;

        return key[..7].SequenceEqual("agents."u8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetCurrentName(string deprecatedKey, [NotNullWhen(true)] out string? currentKey)
    {
        return _deprecatedMap.TryGetValue(deprecatedKey, out currentKey);
    }
}

/// <summary>
///     Provider identifiers per OTel 1.38 gen_ai.provider.name values.
/// </summary>
public static class OtlpGenAiProviders
{
    private static readonly FrozenDictionary<string, string> _hostToProvider =
        new Dictionary<string, string>
        {
            ["api.openai.com"] = "openai",
            ["api.anthropic.com"] = "anthropic",
            ["generativelanguage.googleapis.com"] = "gcp.gemini",
            ["bedrock-runtime"] = "aws.bedrock", // partial match needed
            ["openai.azure.com"] = "azure.openai",
            ["api.cohere.ai"] = "cohere",
            ["api.mistral.ai"] = "mistral"
        }.ToFrozenDictionary();

    public static ReadOnlySpan<byte> OpenAi => "openai"u8;
    public static ReadOnlySpan<byte> Anthropic => "anthropic"u8;
    public static ReadOnlySpan<byte> GcpGemini => "gcp.gemini"u8;
    public static ReadOnlySpan<byte> AwsBedrock => "aws.bedrock"u8;
    public static ReadOnlySpan<byte> AzureOpenAi => "azure.openai"u8;
    public static ReadOnlySpan<byte> Cohere => "cohere"u8;
    public static ReadOnlySpan<byte> Mistral => "mistral"u8;

    public static bool TryDetectFromHost(ReadOnlySpan<char> host, [NotNullWhen(true)] out string? provider)
    {
        var hostStr = host.ToString();
        if (_hostToProvider.TryGetValue(hostStr, out provider)) return true;

        // Partial match for AWS Bedrock
        if (host.Contains("bedrock-runtime", StringComparison.OrdinalIgnoreCase))
        {
            provider = "aws.bedrock";
            return true;
        }

        provider = null;
        return false;
    }
}