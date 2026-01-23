// =============================================================================
// SpanRecord - Manual Type (not generated from TypeSpec)
// =============================================================================
// This type represents a span record for storage and query operations.
// It is a flattened view optimized for database storage, not the OTel wire format.
// =============================================================================

using System;
using System.Text.Json.Serialization;

namespace Qyl.Models;

/// <summary>
/// Span record for storage and query operations.
/// Contains flattened fields for efficient database storage.
/// </summary>
public sealed record SpanRecord
{
    /// <summary>Unique span identifier</summary>
    [JsonPropertyName("span_id")]
    public required Qyl.Common.SpanId SpanId { get; init; }

    /// <summary>Trace identifier</summary>
    [JsonPropertyName("trace_id")]
    public required Qyl.Common.TraceId TraceId { get; init; }

    /// <summary>Parent span identifier (null for root spans)</summary>
    [JsonPropertyName("parent_span_id")]
    public Qyl.Common.SpanId? ParentSpanId { get; init; }

    /// <summary>Session identifier for grouping related traces</summary>
    [JsonPropertyName("session_id")]
    public Qyl.Common.SessionId? SessionId { get; init; }

    /// <summary>Human-readable span name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Span kind</summary>
    [JsonPropertyName("kind")]
    public required Qyl.OTel.Enums.SpanKind Kind { get; init; }

    /// <summary>Start timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("start_time_unix_nano")]
    public required long StartTimeUnixNano { get; init; }

    /// <summary>End timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("end_time_unix_nano")]
    public required long EndTimeUnixNano { get; init; }

    /// <summary>Duration in nanoseconds</summary>
    [JsonPropertyName("duration_ns")]
    public required long DurationNs { get; init; }

    /// <summary>Status code</summary>
    [JsonPropertyName("status_code")]
    public required Qyl.OTel.Enums.SpanStatusCode StatusCode { get; init; }

    /// <summary>Status message</summary>
    [JsonPropertyName("status_message")]
    public string? StatusMessage { get; init; }

    /// <summary>Service name</summary>
    [JsonPropertyName("service_name")]
    public string? ServiceName { get; init; }

    // === GenAI Promoted Fields ===

    /// <summary>GenAI system/provider name (e.g., "openai", "anthropic")</summary>
    [JsonPropertyName("gen_ai_system")]
    public string? GenAiSystem { get; init; }

    /// <summary>Requested model name</summary>
    [JsonPropertyName("gen_ai_request_model")]
    public string? GenAiRequestModel { get; init; }

    /// <summary>Actual model used in response</summary>
    [JsonPropertyName("gen_ai_response_model")]
    public string? GenAiResponseModel { get; init; }

    /// <summary>Input token count</summary>
    [JsonPropertyName("gen_ai_input_tokens")]
    public int? GenAiInputTokens { get; init; }

    /// <summary>Output token count</summary>
    [JsonPropertyName("gen_ai_output_tokens")]
    public int? GenAiOutputTokens { get; init; }

    /// <summary>Temperature setting</summary>
    [JsonPropertyName("gen_ai_temperature")]
    public double? GenAiTemperature { get; init; }

    /// <summary>Stop/finish reason</summary>
    [JsonPropertyName("gen_ai_stop_reason")]
    public string? GenAiStopReason { get; init; }

    /// <summary>Tool name</summary>
    [JsonPropertyName("gen_ai_tool_name")]
    public string? GenAiToolName { get; init; }

    /// <summary>Tool call ID</summary>
    [JsonPropertyName("gen_ai_tool_call_id")]
    public string? GenAiToolCallId { get; init; }

    /// <summary>Cost in USD</summary>
    [JsonPropertyName("gen_ai_cost_usd")]
    public decimal? GenAiCostUsd { get; init; }

    // === JSON Blobs ===

    /// <summary>All span attributes as JSON</summary>
    [JsonPropertyName("attributes_json")]
    public string? AttributesJson { get; init; }

    /// <summary>Resource attributes as JSON</summary>
    [JsonPropertyName("resource_json")]
    public string? ResourceJson { get; init; }

    /// <summary>Record creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}
