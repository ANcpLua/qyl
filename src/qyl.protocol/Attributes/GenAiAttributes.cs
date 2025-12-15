// =============================================================================
// qyl.protocol - OTel Semantic Conventions 1.38 GenAI Attributes
// SINGLE SOURCE OF TRUTH for all gen_ai.* attribute constants
// =============================================================================

using System.Diagnostics.CodeAnalysis;

namespace qyl.protocol.Attributes;

/// <summary>
///     OpenTelemetry Semantic Conventions v1.38 for Generative AI.
///     All gen_ai.* attribute key constants are defined here.
/// </summary>
public static class GenAiAttributes
{
    /// <summary>Activity source name for qyl telemetry.</summary>
    public const string SourceName = "qyl.agents.ai";

    /// <summary>Environment variable for stability opt-in.</summary>
    public const string StabilityEnvVar = "OTEL_SEMCONV_STABILITY_OPT_IN";

    /// <summary>Value to opt-in to latest experimental gen_ai conventions.</summary>
    public const string StabilityOptIn = "gen_ai_latest_experimental";

    /// <summary>OTel schema URL for v1.38.</summary>
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.38.0";

    private const string _prefix = "gen_ai";

    // =========================================================================
    // Provider & Operation
    // =========================================================================

    /// <summary>gen_ai.provider.name - The name of the GenAI provider (e.g., "openai", "anthropic").</summary>
    public const string ProviderName = $"{_prefix}.provider.name";

    /// <summary>gen_ai.operation.name - The operation type (chat, text_completion, etc.).</summary>
    public const string OperationName = $"{_prefix}.operation.name";

    // =========================================================================
    // Request Attributes
    // =========================================================================

    /// <summary>gen_ai.request.model - The model ID requested.</summary>
    public const string RequestModel = $"{_prefix}.request.model";

    /// <summary>gen_ai.request.temperature - Sampling temperature.</summary>
    public const string RequestTemperature = $"{_prefix}.request.temperature";

    /// <summary>gen_ai.request.top_k - Top-k sampling parameter.</summary>
    public const string RequestTopK = $"{_prefix}.request.top_k";

    /// <summary>gen_ai.request.top_p - Nucleus sampling parameter.</summary>
    public const string RequestTopP = $"{_prefix}.request.top_p";

    /// <summary>gen_ai.request.presence_penalty - Presence penalty.</summary>
    public const string RequestPresencePenalty = $"{_prefix}.request.presence_penalty";

    /// <summary>gen_ai.request.frequency_penalty - Frequency penalty.</summary>
    public const string RequestFrequencyPenalty = $"{_prefix}.request.frequency_penalty";

    /// <summary>gen_ai.request.max_tokens - Maximum tokens to generate.</summary>
    public const string RequestMaxTokens = $"{_prefix}.request.max_tokens";

    /// <summary>gen_ai.request.stop_sequences - Stop sequences.</summary>
    public const string RequestStopSequences = $"{_prefix}.request.stop_sequences";

    /// <summary>gen_ai.request.choice.count - Number of choices requested.</summary>
    public const string RequestChoiceCount = $"{_prefix}.request.choice.count";

    /// <summary>gen_ai.request.seed - Random seed for deterministic generation.</summary>
    public const string RequestSeed = $"{_prefix}.request.seed";

    /// <summary>gen_ai.request.encoding_formats - Encoding formats requested.</summary>
    public const string RequestEncodingFormats = $"{_prefix}.request.encoding_formats";

    // =========================================================================
    // Response Attributes
    // =========================================================================

    /// <summary>gen_ai.response.id - Response identifier.</summary>
    public const string ResponseId = $"{_prefix}.response.id";

    /// <summary>gen_ai.response.model - The model that actually served the request.</summary>
    public const string ResponseModel = $"{_prefix}.response.model";

    /// <summary>gen_ai.response.finish_reasons - Finish reasons for the response.</summary>
    public const string ResponseFinishReasons = $"{_prefix}.response.finish_reasons";

    // =========================================================================
    // Usage Attributes
    // =========================================================================

    /// <summary>gen_ai.usage.input_tokens - Number of input tokens.</summary>
    public const string UsageInputTokens = $"{_prefix}.usage.input_tokens";

    /// <summary>gen_ai.usage.output_tokens - Number of output tokens.</summary>
    public const string UsageOutputTokens = $"{_prefix}.usage.output_tokens";

    /// <summary>gen_ai.usage.total_tokens - Total tokens (input + output).</summary>
    public const string UsageTotalTokens = $"{_prefix}.usage.total_tokens";

    // =========================================================================
    // Agent Attributes
    // =========================================================================

    /// <summary>gen_ai.agent.id - Agent identifier.</summary>
    public const string AgentId = $"{_prefix}.agent.id";

    /// <summary>gen_ai.agent.name - Agent name.</summary>
    public const string AgentName = $"{_prefix}.agent.name";

    /// <summary>gen_ai.agent.description - Agent description.</summary>
    public const string AgentDescription = $"{_prefix}.agent.description";

    // =========================================================================
    // Conversation & Content
    // =========================================================================

    /// <summary>gen_ai.conversation.id - Conversation/session identifier.</summary>
    public const string ConversationId = $"{_prefix}.conversation.id";

    /// <summary>gen_ai.system_instructions - System prompt/instructions.</summary>
    public const string SystemInstructions = $"{_prefix}.system_instructions";

    /// <summary>gen_ai.input.messages - Input messages.</summary>
    public const string InputMessages = $"{_prefix}.input.messages";

    /// <summary>gen_ai.output.messages - Output messages.</summary>
    public const string OutputMessages = $"{_prefix}.output.messages";

    /// <summary>gen_ai.output.type - Output type.</summary>
    public const string OutputType = $"{_prefix}.output.type";

    // =========================================================================
    // Tool Attributes
    // =========================================================================

    /// <summary>gen_ai.tool.definitions - Tool definitions available.</summary>
    public const string ToolDefinitions = $"{_prefix}.tool.definitions";

    /// <summary>gen_ai.tool.name - Tool name.</summary>
    public const string ToolName = $"{_prefix}.tool.name";

    /// <summary>gen_ai.tool.description - Tool description.</summary>
    public const string ToolDescription = $"{_prefix}.tool.description";

    /// <summary>gen_ai.tool.type - Tool type.</summary>
    public const string ToolType = $"{_prefix}.tool.type";

    /// <summary>gen_ai.tool.call.id - Tool call identifier.</summary>
    public const string ToolCallId = $"{_prefix}.tool.call.id";

    /// <summary>gen_ai.tool.call.arguments - Tool call arguments.</summary>
    public const string ToolCallArguments = $"{_prefix}.tool.call.arguments";

    /// <summary>gen_ai.tool.call.result - Tool call result.</summary>
    public const string ToolCallResult = $"{_prefix}.tool.call.result";

    // =========================================================================
    // Other Attributes
    // =========================================================================

    /// <summary>gen_ai.data_source.id - Data source identifier.</summary>
    public const string DataSourceId = $"{_prefix}.data_source.id";

    /// <summary>gen_ai.embeddings.dimension.count - Embedding dimension count.</summary>
    public const string EmbeddingsDimensionCount = $"{_prefix}.embeddings.dimension.count";

    /// <summary>gen_ai.token.type - Token type.</summary>
    public const string TokenType = $"{_prefix}.token.type";

    // =========================================================================
    // Evaluation Attributes
    // =========================================================================

    /// <summary>gen_ai.evaluation.name - Evaluation name.</summary>
    public const string EvaluationName = $"{_prefix}.evaluation.name";

    /// <summary>gen_ai.evaluation.score.value - Evaluation score value.</summary>
    public const string EvaluationScoreValue = $"{_prefix}.evaluation.score.value";

    /// <summary>gen_ai.evaluation.score.label - Evaluation score label.</summary>
    public const string EvaluationScoreLabel = $"{_prefix}.evaluation.score.label";

    /// <summary>gen_ai.evaluation.explanation - Evaluation explanation.</summary>
    public const string EvaluationExplanation = $"{_prefix}.evaluation.explanation";

    // =========================================================================
    // Error Attributes (standard OTel)
    // =========================================================================

    /// <summary>error.type - Error type.</summary>
    public const string ErrorType = "error.type";

    /// <summary>error.message - Error message.</summary>
    public const string ErrorMessage = "error.message";

    // =========================================================================
    // Convenience Aliases (for common operation span names)
    // =========================================================================

    /// <summary>Alias for Operations.InvokeAgent.</summary>
    public const string InvokeAgent = Operations.InvokeAgent;

    /// <summary>Alias for Operations.ExecuteTool.</summary>
    public const string ExecuteTool = Operations.ExecuteTool;

    // =========================================================================
    // Helper Methods
    // =========================================================================

    /// <summary>Ensures the latest semantic conventions are opted in via environment variable.</summary>
    public static void EnsureLatestSemantics()
    {
        var current = Environment.GetEnvironmentVariable(StabilityEnvVar);
        if (string.IsNullOrEmpty(current) || !current.Contains(StabilityOptIn, StringComparison.Ordinal))
            Environment.SetEnvironmentVariable(StabilityEnvVar, StabilityOptIn);
    }

    /// <summary>Checks if latest semantic conventions are enabled.</summary>
    public static bool IsLatestSemanticsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(StabilityEnvVar);
        return value?.Contains(StabilityOptIn, StringComparison.Ordinal) == true;
    }

    // =========================================================================
    // Operation Constants
    // =========================================================================

    /// <summary>Standard operation types for gen_ai.operation.name.</summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible",
        Justification = "Intentionally nested to group operation constants")]
    public static class Operations
    {
        public const string Chat = "chat";
        public const string GenerateContent = "generate_content";
        public const string TextCompletion = "text_completion";
        public const string Embeddings = "embeddings";
        public const string InvokeAgent = "invoke_agent";
        public const string ExecuteTool = "execute_tool";
        public const string CreateAgent = "create_agent";
    }

    // =========================================================================
    // Deprecated Attributes (for migration)
    // =========================================================================

    /// <summary>Deprecated attributes from earlier semantic convention versions.</summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible",
        Justification = "Intentionally nested to group deprecated constants")]
    public static class Deprecated
    {
        /// <summary>Deprecated: Use ProviderName instead (semconv 1.38).</summary>
        [Obsolete($"Use {nameof(GenAiAttributes)}.{nameof(ProviderName)} instead (semconv 1.38)")]
        public const string System = "gen_ai.system";

        /// <summary>Deprecated: Use InputMessages instead (semconv 1.38).</summary>
        [Obsolete($"Use {nameof(GenAiAttributes)}.{nameof(InputMessages)} instead (semconv 1.38)")]
        public const string Prompt = "gen_ai.prompt";

        /// <summary>Deprecated: Use OutputMessages instead (semconv 1.38).</summary>
        [Obsolete($"Use {nameof(GenAiAttributes)}.{nameof(OutputMessages)} instead (semconv 1.38)")]
        public const string Completion = "gen_ai.completion";

        /// <summary>Deprecated: Use UsageInputTokens instead (semconv 1.38).</summary>
        [Obsolete($"Use {nameof(GenAiAttributes)}.{nameof(UsageInputTokens)} instead (semconv 1.38)")]
        public const string UsagePromptTokens = "gen_ai.usage.prompt_tokens";

        /// <summary>Deprecated: Use UsageOutputTokens instead (semconv 1.38).</summary>
        [Obsolete($"Use {nameof(GenAiAttributes)}.{nameof(UsageOutputTokens)} instead (semconv 1.38)")]
        public const string UsageCompletionTokens = "gen_ai.usage.completion_tokens";
    }
}
