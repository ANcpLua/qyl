// =============================================================================
// qyl.protocol - GenAI Semantic Convention Attributes
// OTel 1.38 gen_ai.* attribute constants
// Owner: qyl.protocol | Consumers: collector, mcp
// =============================================================================

namespace qyl.protocol.Attributes;

/// <summary>
///     OTel 1.38 GenAI semantic convention attribute keys.
/// </summary>
public static class GenAiAttributes
{
    // ═══════════════════════════════════════════════════════════════════════
    // Schema & Instrumentation
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>OTel 1.38 schema URL.</summary>
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.38.0";

    /// <summary>ActivitySource name for GenAI instrumentation.</summary>
    public const string SourceName = "OpenTelemetry.Instrumentation.GenAI";

    // ═══════════════════════════════════════════════════════════════════════
    // Provider & System (OTel 1.38)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.provider.name - The name of the GenAI provider.</summary>
    public const string ProviderName = "gen_ai.provider.name";

    /// <summary>gen_ai.operation.name - The operation being performed.</summary>
    public const string OperationName = "gen_ai.operation.name";

    // ═══════════════════════════════════════════════════════════════════════
    // Request attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.request.model - The model being requested.</summary>
    public const string RequestModel = "gen_ai.request.model";

    /// <summary>gen_ai.request.temperature - Sampling temperature.</summary>
    public const string RequestTemperature = "gen_ai.request.temperature";

    /// <summary>gen_ai.request.max_tokens - Maximum tokens to generate.</summary>
    public const string RequestMaxTokens = "gen_ai.request.max_tokens";

    /// <summary>gen_ai.request.top_p - Top-p sampling parameter.</summary>
    public const string RequestTopP = "gen_ai.request.top_p";

    /// <summary>gen_ai.request.top_k - Top-k sampling parameter.</summary>
    public const string RequestTopK = "gen_ai.request.top_k";

    /// <summary>gen_ai.request.stop_sequences - Stop sequences.</summary>
    public const string RequestStopSequences = "gen_ai.request.stop_sequences";

    // ═══════════════════════════════════════════════════════════════════════
    // Response attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.response.model - The model used for the response.</summary>
    public const string ResponseModel = "gen_ai.response.model";

    /// <summary>gen_ai.response.finish_reasons - Reasons for completion.</summary>
    public const string ResponseFinishReasons = "gen_ai.response.finish_reasons";

    /// <summary>gen_ai.response.id - Response identifier.</summary>
    public const string ResponseId = "gen_ai.response.id";

    // ═══════════════════════════════════════════════════════════════════════
    // Usage/Token attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.usage.input_tokens - Input tokens consumed.</summary>
    public const string UsageInputTokens = "gen_ai.usage.input_tokens";

    /// <summary>gen_ai.usage.output_tokens - Output tokens generated.</summary>
    public const string UsageOutputTokens = "gen_ai.usage.output_tokens";

    /// <summary>gen_ai.usage.total_tokens - Total tokens (input + output).</summary>
    public const string UsageTotalTokens = "gen_ai.usage.total_tokens";

    // ═══════════════════════════════════════════════════════════════════════
    // Tool/Function calling
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.tool.name - Name of the tool being called.</summary>
    public const string ToolName = "gen_ai.tool.name";

    /// <summary>gen_ai.tool.call.id - Unique identifier for the tool call.</summary>
    public const string ToolCallId = "gen_ai.tool.call.id";

    // ═══════════════════════════════════════════════════════════════════════
    // Conversation/Session
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.conversation.id - Conversation identifier.</summary>
    public const string ConversationId = "gen_ai.conversation.id";

    /// <summary>session.id - Session identifier.</summary>
    public const string SessionId = "session.id";

    // ═══════════════════════════════════════════════════════════════════════
    // Agent operations (qyl extensions)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.agent.name - Agent name.</summary>
    public const string AgentName = "gen_ai.agent.name";

    /// <summary>Operation name for invoking an agent.</summary>
    public const string InvokeAgent = "invoke_agent";

    /// <summary>Operation name for executing a tool.</summary>
    public const string ExecuteTool = "execute_tool";

    /// <summary>gen_ai.error.type - Error type name.</summary>
    public const string ErrorType = "gen_ai.error.type";

    /// <summary>gen_ai.error.message - Error message.</summary>
    public const string ErrorMessage = "gen_ai.error.message";

    /// <summary>Operation type constants.</summary>
    public static class Operations
    {
        /// <summary>invoke_agent - Invoking an agent.</summary>
        public const string InvokeAgent = "invoke_agent";

        /// <summary>execute_tool - Executing a tool.</summary>
        public const string ExecuteTool = "execute_tool";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Deprecated attributes (for migration compatibility)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Deprecated attribute names for backward compatibility.</summary>
    public static class Deprecated
    {
        /// <summary>gen_ai.system - Deprecated, use gen_ai.provider.name.</summary>
        public const string System = "gen_ai.system";

        /// <summary>gen_ai.usage.prompt_tokens - Deprecated, use gen_ai.usage.input_tokens.</summary>
        public const string UsagePromptTokens = "gen_ai.usage.prompt_tokens";

        /// <summary>gen_ai.usage.completion_tokens - Deprecated, use gen_ai.usage.output_tokens.</summary>
        public const string UsageCompletionTokens = "gen_ai.usage.completion_tokens";
    }
}
