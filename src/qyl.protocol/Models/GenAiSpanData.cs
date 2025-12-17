// =============================================================================
// qyl.protocol - GenAiSpanData Model (OTel 1.38)
// Extracted gen_ai.* attributes from a span
// =============================================================================

namespace qyl.protocol.Models;

/// <summary>
///     GenAI-specific span data extracted from gen_ai.* semantic conventions (OTel 1.38).
/// </summary>
public sealed record GenAiSpanData
{
    // =========================================================================
    // Core Required (OTel 1.38)
    // =========================================================================

    /// <summary>gen_ai.provider.name - Provider name (openai, anthropic, etc.).</summary>
    public string? ProviderName { get; init; }

    /// <summary>gen_ai.operation.name - Operation type (chat, text_completion, etc.).</summary>
    public string? OperationName { get; init; }

    /// <summary>gen_ai.request.model - The model ID requested.</summary>
    public string? RequestModel { get; init; }

    /// <summary>gen_ai.response.model - The model that served the request.</summary>
    public string? ResponseModel { get; init; }

    // =========================================================================
    // Usage (OTel 1.38)
    // =========================================================================

    /// <summary>gen_ai.usage.input_tokens - Number of input tokens.</summary>
    public long? InputTokens { get; init; }

    /// <summary>gen_ai.usage.output_tokens - Number of output tokens.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>Total tokens (computed: input + output).</summary>
    public long? TotalTokens => InputTokens.HasValue || OutputTokens.HasValue
        ? (InputTokens ?? 0) + (OutputTokens ?? 0)
        : null;

    // =========================================================================
    // Request Parameters (OTel 1.38)
    // =========================================================================

    /// <summary>gen_ai.request.temperature - Sampling temperature.</summary>
    public double? Temperature { get; init; }

    /// <summary>gen_ai.request.top_p - Top-p sampling parameter.</summary>
    public double? TopP { get; init; }

    /// <summary>gen_ai.request.top_k - Top-k sampling parameter.</summary>
    public double? TopK { get; init; }

    /// <summary>gen_ai.request.max_tokens - Maximum tokens requested.</summary>
    public long? MaxTokens { get; init; }

    /// <summary>gen_ai.request.seed - Random seed for deterministic output.</summary>
    public long? Seed { get; init; }

    /// <summary>gen_ai.request.frequency_penalty - Frequency penalty.</summary>
    public double? FrequencyPenalty { get; init; }

    /// <summary>gen_ai.request.presence_penalty - Presence penalty.</summary>
    public double? PresencePenalty { get; init; }

    /// <summary>gen_ai.request.choice.count - Number of choices requested.</summary>
    public int? ChoiceCount { get; init; }

    // =========================================================================
    // Response (OTel 1.38)
    // =========================================================================

    /// <summary>gen_ai.response.id - Response identifier.</summary>
    public string? ResponseId { get; init; }

    /// <summary>gen_ai.response.finish_reasons - Finish reasons.</summary>
    public IReadOnlyList<string>? FinishReasons { get; init; }

    // =========================================================================
    // Agent (OTel 1.38)
    // =========================================================================

    /// <summary>gen_ai.agent.id - Agent identifier.</summary>
    public string? AgentId { get; init; }

    /// <summary>gen_ai.agent.name - Agent name (for agent operations).</summary>
    public string? AgentName { get; init; }

    // =========================================================================
    // Tool (OTel 1.38)
    // =========================================================================

    /// <summary>gen_ai.tool.name - Tool name (for tool operations).</summary>
    public string? ToolName { get; init; }

    /// <summary>gen_ai.tool.call.id - Tool call identifier.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>gen_ai.tool.type - Tool type (function, extension, datastore).</summary>
    public string? ToolType { get; init; }

    // =========================================================================
    // Conversation (OTel 1.38)
    // =========================================================================

    /// <summary>gen_ai.conversation.id - Conversation/session identifier.</summary>
    public string? ConversationId { get; init; }

    // =========================================================================
    // Computed/Derived
    // =========================================================================

    /// <summary>Indicates if this span represents a GenAI operation.</summary>
    public bool IsGenAi =>
        !string.IsNullOrEmpty(ProviderName) ||
        !string.IsNullOrEmpty(OperationName) ||
        InputTokens.HasValue ||
        OutputTokens.HasValue;

    /// <summary>Indicates if this is a tool execution operation.</summary>
    public bool IsToolCall => !string.IsNullOrEmpty(ToolName) || !string.IsNullOrEmpty(ToolCallId);

    /// <summary>Indicates if this is an agent operation.</summary>
    public bool IsAgentOperation => !string.IsNullOrEmpty(AgentId) || !string.IsNullOrEmpty(AgentName);
}
