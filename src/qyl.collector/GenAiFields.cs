namespace qyl.collector;

/// <summary>
///     Extracted GenAI span data.
/// </summary>
public sealed record GenAiFields
{
    public static readonly GenAiFields Empty = new();

    /// <summary>Provider name (e.g., "anthropic", "openai", "google").</summary>
    public string? ProviderName { get; init; }

    /// <summary>Operation name (e.g., "chat", "completion", "embedding").</summary>
    public string? OperationName { get; init; }

    /// <summary>Model ID from the request.</summary>
    public string? RequestModel { get; init; }

    /// <summary>Model ID from the response (may differ from request).</summary>
    public string? ResponseModel { get; init; }

    /// <summary>Number of input/prompt tokens.</summary>
    public long? InputTokens { get; init; }

    /// <summary>Number of output/completion tokens.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>Total tokens (if provided explicitly).</summary>
    public long? TotalTokens { get; init; }

    /// <summary>Request temperature parameter.</summary>
    public double? Temperature { get; init; }

    /// <summary>Request max tokens parameter.</summary>
    public long? MaxTokens { get; init; }

    /// <summary>Response finish/stop reason.</summary>
    public string? FinishReason { get; init; }

    /// <summary>Estimated cost in USD.</summary>
    public double? CostUsd { get; init; }

    /// <summary>Session/conversation ID.</summary>
    public string? SessionId { get; init; }

    /// <summary>Tool name (for tool calls).</summary>
    public string? ToolName { get; init; }

    /// <summary>Tool call ID (for tool calls).</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Effective model (response model if available, else request model).</summary>
    public string? Model => ResponseModel ?? RequestModel;

    /// <summary>Whether this span has token usage data.</summary>
    public bool HasTokenUsage => InputTokens.HasValue || OutputTokens.HasValue;

    /// <summary>Whether this represents a GenAI span.</summary>
    public bool IsGenAi => ProviderName is not null || Model is not null;

    /// <summary>Whether this is a tool call span.</summary>
    public bool IsToolCall => ToolName is not null || ToolCallId is not null;

    /// <summary>Computed total tokens (sum of input + output if total not provided).</summary>
    public long ComputedTotalTokens => TotalTokens ?? (InputTokens ?? 0) + (OutputTokens ?? 0);
}