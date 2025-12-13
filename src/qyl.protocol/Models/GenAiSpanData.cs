// =============================================================================
// qyl.protocol - GenAiSpanData Model
// Extracted gen_ai.* attributes from a span
// =============================================================================

namespace Qyl.Protocol.Models;

/// <summary>
///     GenAI-specific span data extracted from gen_ai.* semantic conventions.
/// </summary>
public sealed record GenAiSpanData
{
    /// <summary>gen_ai.provider.name - Provider name (openai, anthropic, etc.).</summary>
    public string? ProviderName { get; init; }

    /// <summary>gen_ai.operation.name - Operation type (chat, text_completion, etc.).</summary>
    public string? OperationName { get; init; }

    /// <summary>gen_ai.request.model - The model ID requested.</summary>
    public string? RequestModel { get; init; }

    /// <summary>gen_ai.response.model - The model that served the request.</summary>
    public string? ResponseModel { get; init; }

    /// <summary>gen_ai.usage.input_tokens - Number of input tokens.</summary>
    public long? InputTokens { get; init; }

    /// <summary>gen_ai.usage.output_tokens - Number of output tokens.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>gen_ai.usage.total_tokens - Total tokens (input + output).</summary>
    public long? TotalTokens { get; init; }

    /// <summary>gen_ai.request.temperature - Sampling temperature.</summary>
    public double? Temperature { get; init; }

    /// <summary>gen_ai.request.max_tokens - Maximum tokens requested.</summary>
    public long? MaxTokens { get; init; }

    /// <summary>gen_ai.response.id - Response identifier.</summary>
    public string? ResponseId { get; init; }

    /// <summary>gen_ai.response.finish_reasons - Finish reasons.</summary>
    public string[]? FinishReasons { get; init; }

    /// <summary>gen_ai.agent.name - Agent name (for agent operations).</summary>
    public string? AgentName { get; init; }

    /// <summary>gen_ai.tool.name - Tool name (for tool operations).</summary>
    public string? ToolName { get; init; }

    /// <summary>Indicates if this span represents a GenAI operation.</summary>
    public bool IsGenAi =>
        !string.IsNullOrEmpty(ProviderName) ||
        !string.IsNullOrEmpty(OperationName) ||
        InputTokens.HasValue ||
        OutputTokens.HasValue;
}