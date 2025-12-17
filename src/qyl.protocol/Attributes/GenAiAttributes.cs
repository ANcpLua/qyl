// =============================================================================
// qyl.protocol - OTel Semantic Conventions 1.38 GenAI Attributes
// SINGLE SOURCE OF TRUTH for all gen_ai.* attribute constants
// =============================================================================

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

    private const string Prefix = "gen_ai";

    // =========================================================================
    // Provider & Operation
    // =========================================================================

    /// <summary>gen_ai.provider.name - The name of the GenAI provider (e.g., "openai", "anthropic").</summary>
    public const string ProviderName = $"{Prefix}.provider.name";

    /// <summary>gen_ai.operation.name - The operation type (chat, text_completion, etc.).</summary>
    public const string OperationName = $"{Prefix}.operation.name";

    // =========================================================================
    // Request Attributes
    // =========================================================================

    /// <summary>gen_ai.request.model - The model ID requested.</summary>
    public const string RequestModel = $"{Prefix}.request.model";

    /// <summary>gen_ai.request.temperature - Sampling temperature.</summary>
    public const string RequestTemperature = $"{Prefix}.request.temperature";

    /// <summary>gen_ai.request.top_k - Top-k sampling parameter.</summary>
    public const string RequestTopK = $"{Prefix}.request.top_k";

    /// <summary>gen_ai.request.top_p - Nucleus sampling parameter.</summary>
    public const string RequestTopP = $"{Prefix}.request.top_p";

    /// <summary>gen_ai.request.presence_penalty - Presence penalty.</summary>
    public const string RequestPresencePenalty = $"{Prefix}.request.presence_penalty";

    /// <summary>gen_ai.request.frequency_penalty - Frequency penalty.</summary>
    public const string RequestFrequencyPenalty = $"{Prefix}.request.frequency_penalty";

    /// <summary>gen_ai.request.max_tokens - Maximum tokens to generate.</summary>
    public const string RequestMaxTokens = $"{Prefix}.request.max_tokens";

    /// <summary>gen_ai.request.stop_sequences - Stop sequences.</summary>
    public const string RequestStopSequences = $"{Prefix}.request.stop_sequences";

    /// <summary>gen_ai.request.choice.count - Number of choices requested.</summary>
    public const string RequestChoiceCount = $"{Prefix}.request.choice.count";

    /// <summary>gen_ai.request.seed - Random seed for deterministic generation.</summary>
    public const string RequestSeed = $"{Prefix}.request.seed";

    /// <summary>gen_ai.request.encoding_formats - Encoding formats requested.</summary>
    public const string RequestEncodingFormats = $"{Prefix}.request.encoding_formats";

    // =========================================================================
    // Response Attributes
    // =========================================================================

    /// <summary>gen_ai.response.id - Response identifier.</summary>
    public const string ResponseId = $"{Prefix}.response.id";

    /// <summary>gen_ai.response.model - The model that actually served the request.</summary>
    public const string ResponseModel = $"{Prefix}.response.model";

    /// <summary>gen_ai.response.finish_reasons - Finish reasons for the response.</summary>
    public const string ResponseFinishReasons = $"{Prefix}.response.finish_reasons";

    // =========================================================================
    // Usage Attributes
    // =========================================================================

    /// <summary>gen_ai.usage.input_tokens - Number of input tokens.</summary>
    public const string UsageInputTokens = $"{Prefix}.usage.input_tokens";

    /// <summary>gen_ai.usage.output_tokens - Number of output tokens.</summary>
    public const string UsageOutputTokens = $"{Prefix}.usage.output_tokens";

    /// <summary>gen_ai.usage.total_tokens - Total tokens (input + output).</summary>
    public const string UsageTotalTokens = $"{Prefix}.usage.total_tokens";

    // =========================================================================
    // Agent Attributes
    // =========================================================================

    /// <summary>gen_ai.agent.id - Agent identifier.</summary>
    public const string AgentId = $"{Prefix}.agent.id";

    /// <summary>gen_ai.agent.name - Agent name.</summary>
    public const string AgentName = $"{Prefix}.agent.name";

    /// <summary>gen_ai.agent.description - Agent description.</summary>
    public const string AgentDescription = $"{Prefix}.agent.description";

    // =========================================================================
    // Conversation & Content
    // =========================================================================

    /// <summary>gen_ai.conversation.id - Conversation/session identifier.</summary>
    public const string ConversationId = $"{Prefix}.conversation.id";

    /// <summary>gen_ai.system_instructions - System prompt/instructions.</summary>
    public const string SystemInstructions = $"{Prefix}.system_instructions";

    /// <summary>gen_ai.input.messages - Input messages.</summary>
    public const string InputMessages = $"{Prefix}.input.messages";

    /// <summary>gen_ai.output.messages - Output messages.</summary>
    public const string OutputMessages = $"{Prefix}.output.messages";

    /// <summary>gen_ai.output.type - Output type.</summary>
    public const string OutputType = $"{Prefix}.output.type";

    // =========================================================================
    // Tool Attributes
    // =========================================================================

    /// <summary>gen_ai.tool.definitions - Tool definitions available.</summary>
    public const string ToolDefinitions = $"{Prefix}.tool.definitions";

    /// <summary>gen_ai.tool.name - Tool name.</summary>
    public const string ToolName = $"{Prefix}.tool.name";

    /// <summary>gen_ai.tool.description - Tool description.</summary>
    public const string ToolDescription = $"{Prefix}.tool.description";

    /// <summary>gen_ai.tool.type - Tool type.</summary>
    public const string ToolType = $"{Prefix}.tool.type";

    /// <summary>gen_ai.tool.call.id - Tool call identifier.</summary>
    public const string ToolCallId = $"{Prefix}.tool.call.id";

    /// <summary>gen_ai.tool.call.arguments - Tool call arguments.</summary>
    public const string ToolCallArguments = $"{Prefix}.tool.call.arguments";

    /// <summary>gen_ai.tool.call.result - Tool call result.</summary>
    public const string ToolCallResult = $"{Prefix}.tool.call.result";

    // =========================================================================
    // Server Attributes (Standard OTel)
    // =========================================================================

    /// <summary>server.address - GenAI server address (Recommended).</summary>
    public const string ServerAddress = "server.address";

    /// <summary>server.port - GenAI server port (Conditionally Required).</summary>
    public const string ServerPort = "server.port";

    // =========================================================================
    // Other Attributes
    // =========================================================================

    /// <summary>gen_ai.data_source.id - Data source identifier.</summary>
    public const string DataSourceId = $"{Prefix}.data_source.id";

    /// <summary>gen_ai.embeddings.dimension.count - Embedding dimension count.</summary>
    public const string EmbeddingsDimensionCount = $"{Prefix}.embeddings.dimension.count";

    /// <summary>gen_ai.token.type - Token type.</summary>
    public const string TokenType = $"{Prefix}.token.type";

    // =========================================================================
    // Evaluation Attributes
    // =========================================================================

    /// <summary>gen_ai.evaluation.name - Evaluation name.</summary>
    public const string EvaluationName = $"{Prefix}.evaluation.name";

    /// <summary>gen_ai.evaluation.score.value - Evaluation score value.</summary>
    public const string EvaluationScoreValue = $"{Prefix}.evaluation.score.value";

    /// <summary>gen_ai.evaluation.score.label - Evaluation score label.</summary>
    public const string EvaluationScoreLabel = $"{Prefix}.evaluation.score.label";

    /// <summary>gen_ai.evaluation.explanation - Evaluation explanation.</summary>
    public const string EvaluationExplanation = $"{Prefix}.evaluation.explanation";

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

    // =========================================================================
    // Event Names (OTel v1.38)
    // =========================================================================

    /// <summary>Semantic names for GenAI Events (structured logs).</summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible",
        Justification = "Intentionally nested to group event constants")]
    public static class Events
    {
        /// <summary>Event containing full input/output details for an inference operation.</summary>
        public const string OperationDetails = "gen_ai.client.inference.operation.details";

        /// <summary>Event containing evaluation results.</summary>
        public const string EvaluationResult = "gen_ai.evaluation.result";
    }

    // =========================================================================
    // Metric Names (OTel v1.38)
    // =========================================================================

    /// <summary>Semantic names for GenAI Metrics.</summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible",
        Justification = "Intentionally nested to group metric constants")]
    public static class Metrics
    {
        /// <summary>Histogram: Number of input and output tokens used.</summary>
        public const string TokenUsage = "gen_ai.client.token.usage";

        /// <summary>Histogram: GenAI operation duration in seconds.</summary>
        public const string OperationDuration = "gen_ai.client.operation.duration";

        /// <summary>Histogram: Server time per output token (decode phase) in seconds.</summary>
        public const string ServerTimePerOutputToken = "gen_ai.server.time_per_output_token";

        /// <summary>Histogram: Server time to first token (prefill phase) in seconds.</summary>
        public const string ServerTimeToFirstToken = "gen_ai.server.time_to_first_token";
    }
}
