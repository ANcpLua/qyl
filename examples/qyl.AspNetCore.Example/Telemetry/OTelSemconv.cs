using System.Collections.Frozen;

namespace TelemetryLab.Net10.Api.Domain.Telemetry;

/// <summary>
/// OpenTelemetry Semantic Conventions v1.38 - Gen AI Attributes
///
/// This demonstrates the pattern from qyl's generated code:
/// - String constants for attribute keys
/// - UTF-8 spans for zero-allocation parsing
/// - SearchValues&lt;string&gt; for O(1) prefix matching
/// - FrozenSet/FrozenDictionary for immutable lookups
/// - Well-known values with validation
/// </summary>
public static class OTelSemconv
{
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.38.0";

    // ═══════════════════════════════════════════════════════════════════════════
    // Gen AI Attribute Keys (OTel 1.38)
    // ═══════════════════════════════════════════════════════════════════════════

    // Core
    public const string OperationName = "gen_ai.operation.name";
    public const string ProviderName = "gen_ai.provider.name";

    // Request
    public const string RequestModel = "gen_ai.request.model";
    public const string RequestTemperature = "gen_ai.request.temperature";
    public const string RequestMaxTokens = "gen_ai.request.max_tokens";
    public const string RequestTopP = "gen_ai.request.top_p";
    public const string RequestTopK = "gen_ai.request.top_k";
    public const string RequestStopSequences = "gen_ai.request.stop_sequences";
    public const string RequestFrequencyPenalty = "gen_ai.request.frequency_penalty";
    public const string RequestPresencePenalty = "gen_ai.request.presence_penalty";
    public const string RequestPerOutputSequence = "gen_ai.request.per_output_sequence";
    public const string RequestSeed = "gen_ai.request.seed";

    // Response
    public const string ResponseModel = "gen_ai.response.model";
    public const string ResponseFinishReasons = "gen_ai.response.finish_reasons";
    public const string ResponseId = "gen_ai.response.id";

    // Usage
    public const string UsageInputTokens = "gen_ai.usage.input_tokens";
    public const string UsageOutputTokens = "gen_ai.usage.output_tokens";

    // Agent (NEW in 1.38)
    public const string AgentId = "gen_ai.agent.id";
    public const string AgentName = "gen_ai.agent.name";
    public const string AgentDescription = "gen_ai.agent.description";

    // Tool
    public const string ToolName = "gen_ai.tool.name";
    public const string ToolCallId = "gen_ai.tool.call.id";
    public const string ToolDescription = "gen_ai.tool.description";

    // ═══════════════════════════════════════════════════════════════════════════
    // All Attribute Keys (for validation)
    // ═══════════════════════════════════════════════════════════════════════════

    public static readonly FrozenSet<string> AllKeys = FrozenSet.ToFrozenSet([
        OperationName, ProviderName,
        RequestModel, RequestTemperature, RequestMaxTokens, RequestTopP,
        RequestTopK, RequestStopSequences, RequestFrequencyPenalty,
        RequestPresencePenalty, RequestPerOutputSequence, RequestSeed,
        ResponseModel, ResponseFinishReasons, ResponseId,
        UsageInputTokens, UsageOutputTokens,
        AgentId, AgentName, AgentDescription,
        ToolName, ToolCallId, ToolDescription
    ]);

    // ═══════════════════════════════════════════════════════════════════════════
    // Prefix Matching for Gen AI Attributes
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prefixes for Gen AI and Agent attributes.
    /// </summary>
    private static readonly FrozenSet<string> GenAiAndAgentPrefixes =
        FrozenSet.ToFrozenSet(["gen_ai.", "agents."]);

    /// <summary>
    /// Check if an attribute key is a Gen AI or Agent attribute.
    /// </summary>
    public static bool IsGenAiOrAgentAttribute(string key)
        => key.StartsWith("gen_ai.", StringComparison.Ordinal) ||
           key.StartsWith("agents.", StringComparison.Ordinal);

    /// <summary>
    /// Check if an attribute key is a Gen AI or Agent attribute (span version).
    /// </summary>
    public static bool IsGenAiOrAgentAttribute(ReadOnlySpan<char> key)
        => key.StartsWith("gen_ai.".AsSpan(), StringComparison.Ordinal) ||
           key.StartsWith("agents.".AsSpan(), StringComparison.Ordinal);

    // ═══════════════════════════════════════════════════════════════════════════
    // Well-Known Values
    // ═══════════════════════════════════════════════════════════════════════════

    public static class OperationNames
    {
        public const string Chat = "chat";
        public const string TextCompletion = "text_completion";
        public const string Embeddings = "embeddings";
        public const string ImageGeneration = "image_generation";
        public const string AudioTranscription = "audio_transcription";
        public const string AudioTranslation = "audio_translation";
        public const string TextToSpeech = "text_to_speech";

        public static readonly FrozenSet<string> ValidValues = FrozenSet.ToFrozenSet([
            Chat, TextCompletion, Embeddings, ImageGeneration,
            AudioTranscription, AudioTranslation, TextToSpeech
        ]);

        public static bool IsValid(string? value)
            => value is not null && ValidValues.Contains(value);
    }

    public static class ProviderNames
    {
        public const string OpenAi = "openai";
        public const string Anthropic = "anthropic";
        public const string Azure = "azure";
        public const string Google = "google";
        public const string Cohere = "cohere";
        public const string Mistral = "mistral";
        public const string Aws = "aws";

        public static readonly FrozenSet<string> ValidValues = FrozenSet.ToFrozenSet([
            OpenAi, Anthropic, Azure, Google, Cohere, Mistral, Aws
        ]);

        public static bool IsValid(string? value)
            => value is not null && ValidValues.Contains(value);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Deprecated Attribute Migrations
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Maps deprecated attribute names to their current equivalents.
    /// </summary>
    public static readonly FrozenDictionary<string, string> Migrations =
        new Dictionary<string, string>
        {
            ["gen_ai.system"] = ProviderName,  // Deprecated → gen_ai.provider.name
        }.ToFrozenDictionary();

    /// <summary>
    /// Normalizes an attribute key to the current schema.
    /// Returns the current name if the key is deprecated, otherwise returns the key unchanged.
    /// </summary>
    public static string Normalize(string key)
        => Migrations.GetValueOrDefault(key, key);
}