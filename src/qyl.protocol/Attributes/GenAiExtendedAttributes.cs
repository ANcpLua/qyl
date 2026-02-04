// =============================================================================
// qyl.protocol - Extended GenAI Attributes (qyl-specific)
// Additional telemetry attributes beyond OTel 1.39 semconv
// These are qyl extensions, not part of official OTel spec
// =============================================================================

namespace qyl.protocol.Attributes;

/// <summary>
///     qyl-specific GenAI telemetry attribute keys.
///     These extend OTel 1.39 semconv with additional observability data.
/// </summary>
public static class GenAiExtendedAttributes
{
    // ═══════════════════════════════════════════════════════════════════════
    // Cost Tracking (qyl extension)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>qyl.gen_ai.cost.usd - Calculated cost in USD for the request.</summary>
    public const string CostUsd = "qyl.gen_ai.cost.usd";

    /// <summary>qyl.gen_ai.cost.input_usd - Cost of input tokens in USD.</summary>
    public const string CostInputUsd = "qyl.gen_ai.cost.input_usd";

    /// <summary>qyl.gen_ai.cost.output_usd - Cost of output tokens in USD.</summary>
    public const string CostOutputUsd = "qyl.gen_ai.cost.output_usd";

    // ═══════════════════════════════════════════════════════════════════════
    // Streaming Metrics (qyl extension - supplements OTel metrics)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>qyl.gen_ai.streaming.time_to_first_token_ms - Time to first token in milliseconds.</summary>
    public const string TimeToFirstTokenMs = "qyl.gen_ai.streaming.time_to_first_token_ms";

    /// <summary>qyl.gen_ai.streaming.total_chunks - Total number of streaming chunks received.</summary>
    public const string StreamingTotalChunks = "qyl.gen_ai.streaming.total_chunks";

    /// <summary>qyl.gen_ai.streaming.throughput_tokens_per_sec - Streaming throughput in tokens/second.</summary>
    public const string StreamingThroughput = "qyl.gen_ai.streaming.throughput_tokens_per_sec";

    // ═══════════════════════════════════════════════════════════════════════
    // Context Pressure (qyl extension)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>qyl.gen_ai.context.utilization - Context window utilization (0.0 - 1.0).</summary>
    public const string ContextUtilization = "qyl.gen_ai.context.utilization";

    /// <summary>qyl.gen_ai.context.max_tokens - Maximum context window for the model.</summary>
    public const string ContextMaxTokens = "qyl.gen_ai.context.max_tokens";

    /// <summary>qyl.gen_ai.context.available_tokens - Remaining tokens available for output.</summary>
    public const string ContextAvailableTokens = "qyl.gen_ai.context.available_tokens";

    // ═══════════════════════════════════════════════════════════════════════
    // Tool Call Aggregates (qyl extension)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>qyl.gen_ai.tool.call_count - Number of tool calls in the response.</summary>
    public const string ToolCallCount = "qyl.gen_ai.tool.call_count";

    /// <summary>qyl.gen_ai.tool.names - Comma-separated list of tool names called.</summary>
    public const string ToolNames = "qyl.gen_ai.tool.names";

    // ═══════════════════════════════════════════════════════════════════════
    // Schema Validation (qyl extension)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>qyl.gen_ai.schema.requested - Name of the requested output schema.</summary>
    public const string SchemaRequested = "qyl.gen_ai.schema.requested";

    /// <summary>qyl.gen_ai.schema.valid - Whether the output matched the requested schema.</summary>
    public const string SchemaValid = "qyl.gen_ai.schema.valid";

    /// <summary>qyl.gen_ai.schema.error - Schema validation error message if invalid.</summary>
    public const string SchemaError = "qyl.gen_ai.schema.error";

    // ═══════════════════════════════════════════════════════════════════════
    // Metrics names (qyl extension)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>qyl GenAI metrics names.</summary>
    public static class Metrics
    {
        /// <summary>qyl.gen_ai.cost - Total cost per request in USD.</summary>
        public const string Cost = "qyl.gen_ai.cost";

        /// <summary>qyl.gen_ai.time_to_first_token - Time to first token for streaming.</summary>
        public const string TimeToFirstToken = "qyl.gen_ai.time_to_first_token";

        /// <summary>qyl.gen_ai.tool_calls - Number of tool calls executed.</summary>
        public const string ToolCalls = "qyl.gen_ai.tool_calls";

        /// <summary>qyl.gen_ai.context_utilization - Context window utilization ratio.</summary>
        public const string ContextUtilization = "qyl.gen_ai.context_utilization";

        /// <summary>qyl.gen_ai.schema_validation_failures - Count of schema validation failures.</summary>
        public const string SchemaValidationFailures = "qyl.gen_ai.schema_validation_failures";
    }
}
