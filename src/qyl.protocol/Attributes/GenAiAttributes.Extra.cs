// =============================================================================
// qyl.protocol - GenAiAttributes Extensions (non-generated)
// These are additional constants and helpers not derived from the attribute dictionary
// =============================================================================

namespace qyl.protocol.Attributes;

/// <summary>
/// Extension of GenAiAttributes with constants not generated from the schema dictionary.
/// </summary>
public static partial class GenAiAttributes
{
    // =========================================================================
    // Telemetry Configuration (Static Metadata)
    // =========================================================================

    /// <summary>Activity source name for qyl telemetry.</summary>
    public const string SourceName = "qyl.agents.ai";

    /// <summary>OTel schema URL for v1.38.</summary>
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.38.0";

    /// <summary>Environment variable for stability opt-in.</summary>
    public const string StabilityEnvVar = "OTEL_SEMCONV_STABILITY_OPT_IN";

    /// <summary>Value to opt-in to latest experimental gen_ai conventions.</summary>
    public const string StabilityOptIn = "gen_ai_latest_experimental";

    // =========================================================================
    // Short Aliases (for convenience)
    // =========================================================================

    /// <summary>gen_ai.operation.name - The operation being performed.</summary>
    public const string OperationName = GenAiOperationName;

    /// <summary>gen_ai.provider.name - Provider identifier.</summary>
    public const string ProviderName = GenAiProviderName;

    /// <summary>gen_ai.request.model - Requested model name.</summary>
    public const string RequestModel = GenAiRequestModel;

    /// <summary>gen_ai.response.model - Actual model that responded.</summary>
    public const string ResponseModel = GenAiResponseModel;

    /// <summary>gen_ai.response.id - Unique completion identifier.</summary>
    public const string ResponseId = GenAiResponseId;

    /// <summary>gen_ai.usage.input_tokens - Input/prompt token count.</summary>
    public const string UsageInputTokens = GenAiUsageInputTokens;

    /// <summary>gen_ai.usage.output_tokens - Output/completion token count.</summary>
    public const string UsageOutputTokens = GenAiUsageOutputTokens;

    /// <summary>gen_ai.agent.id - Unique agent identifier.</summary>
    public const string AgentId = GenAiAgentId;

    /// <summary>gen_ai.agent.name - Human-readable agent name.</summary>
    public const string AgentName = GenAiAgentName;

    /// <summary>gen_ai.tool.name - Tool name.</summary>
    public const string ToolName = GenAiToolName;

    /// <summary>gen_ai.tool.call.id - Tool call identifier.</summary>
    public const string ToolCallId = GenAiToolCallId;

    /// <summary>gen_ai.conversation.id - Session/thread identifier.</summary>
    public const string ConversationId = GenAiConversationId;

    /// <summary>gen_ai.request.temperature - Temperature setting.</summary>
    public const string RequestTemperature = GenAiRequestTemperature;

    /// <summary>gen_ai.request.max_tokens - Max tokens to generate.</summary>
    public const string RequestMaxTokens = GenAiRequestMaxTokens;

    /// <summary>gen_ai.response.finish_reasons - Array of stop reasons.</summary>
    public const string ResponseFinishReasons = GenAiResponseFinishReasons;

    /// <summary>gen_ai.usage.total_tokens - Total token count (if provided).</summary>
    public const string UsageTotalTokens = "gen_ai.usage.total_tokens";

    // =========================================================================
    // Error Attributes (OTel standard, not gen_ai.*)
    // =========================================================================

    /// <summary>error.type - Error type name.</summary>
    public const string ErrorType = "error.type";

    /// <summary>error.message - Error message.</summary>
    public const string ErrorMessage = "error.message";

    // =========================================================================
    // Operation Values (for activity names)
    // =========================================================================

    /// <summary>invoke_agent - Operation name for agent invocation.</summary>
    public const string InvokeAgent = "invoke_agent";

    /// <summary>execute_tool - Operation name for tool execution.</summary>
    public const string ExecuteTool = "execute_tool";

    /// <summary>Well-known operation names.</summary>
    public static class Operations
    {
        public const string InvokeAgent = "invoke_agent";
        public const string ExecuteTool = "execute_tool";
        public const string Chat = "chat";
        public const string TextCompletion = "text_completion";
        public const string Embeddings = "embeddings";
        public const string GenerateContent = "generate_content";
        public const string CreateAgent = "create_agent";
    }

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
    // Deprecated Attribute Aliases (for backward compatibility)
    // =========================================================================

    /// <summary>Deprecated attribute names for backward compatibility.</summary>
    public static class Deprecated
    {
        /// <summary>gen_ai.system - DEPRECATED: Use gen_ai.provider.name</summary>
        [Obsolete("Use GenAiAttributes.ProviderName instead")]
        public const string System = "gen_ai.system";

        /// <summary>gen_ai.usage.prompt_tokens - DEPRECATED: Use gen_ai.usage.input_tokens</summary>
        [Obsolete("Use GenAiAttributes.UsageInputTokens instead")]
        public const string UsagePromptTokens = "gen_ai.usage.prompt_tokens";

        /// <summary>gen_ai.usage.completion_tokens - DEPRECATED: Use gen_ai.usage.output_tokens</summary>
        [Obsolete("Use GenAiAttributes.UsageOutputTokens instead")]
        public const string UsageCompletionTokens = "gen_ai.usage.completion_tokens";

        /// <summary>gen_ai.prompt - DEPRECATED: Use gen_ai.input.messages</summary>
        [Obsolete("Use GenAiAttributes.GenAiInputMessages instead")]
        public const string Prompt = "gen_ai.prompt";

        /// <summary>gen_ai.completion - DEPRECATED: Use gen_ai.output.messages</summary>
        [Obsolete("Use GenAiAttributes.GenAiOutputMessages instead")]
        public const string Completion = "gen_ai.completion";
    }
}
