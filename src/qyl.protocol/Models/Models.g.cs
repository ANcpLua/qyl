// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    schema/generated/openapi.yaml
//     Generated: 2026-01-13T06:07:10.6799280+00:00
//     Models for Qyl.Models
// =============================================================================
// To modify: update TypeSpec in schema/ then run: nuke Generate
// =============================================================================

#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Models;

/// <summary>Extracted GenAI attributes from a span</summary>
public sealed record GenAiSpanData
{
    /// <summary>Provider/system name</summary>
    [JsonPropertyName("system")]
    public string? System { get; init; }

    /// <summary>Requested model</summary>
    [JsonPropertyName("requestModel")]
    public string? RequestModel { get; init; }

    /// <summary>Response model</summary>
    [JsonPropertyName("responseModel")]
    public string? ResponseModel { get; init; }

    /// <summary>Input tokens</summary>
    [JsonPropertyName("inputTokens")]
    public global::Qyl.Common.TokenCount? InputTokens { get; init; }

    /// <summary>Output tokens</summary>
    [JsonPropertyName("outputTokens")]
    public global::Qyl.Common.TokenCount? OutputTokens { get; init; }

    /// <summary>Temperature</summary>
    [JsonPropertyName("temperature")]
    public global::Qyl.Common.Temperature? Temperature { get; init; }

    /// <summary>Finish reason</summary>
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; init; }

    /// <summary>Tool name</summary>
    [JsonPropertyName("toolName")]
    public string? ToolName { get; init; }

    /// <summary>Tool call ID</summary>
    [JsonPropertyName("toolCallId")]
    public string? ToolCallId { get; init; }

    /// <summary>Estimated cost</summary>
    [JsonPropertyName("costUsd")]
    public global::Qyl.Common.CostUsd? CostUsd { get; init; }

}

/// <summary>Session summary with aggregated metrics</summary>
public sealed record SessionSummary
{
    /// <summary>Session identifier</summary>
    [JsonPropertyName("sessionId")]
    public global::Qyl.Common.SessionId SessionId { get; init; }

    /// <summary>First span timestamp</summary>
    [JsonPropertyName("startTime")]
    public global::Qyl.Common.UnixNano StartTime { get; init; }

    /// <summary>Last span timestamp</summary>
    [JsonPropertyName("endTime")]
    public global::Qyl.Common.UnixNano EndTime { get; init; }

    /// <summary>Total span count</summary>
    [JsonPropertyName("spanCount")]
    public global::Qyl.Common.Count SpanCount { get; init; }

    /// <summary>Error span count</summary>
    [JsonPropertyName("errorCount")]
    public global::Qyl.Common.Count ErrorCount { get; init; }

    /// <summary>Total input tokens</summary>
    [JsonPropertyName("totalInputTokens")]
    public global::Qyl.Common.TokenCount TotalInputTokens { get; init; }

    /// <summary>Total output tokens</summary>
    [JsonPropertyName("totalOutputTokens")]
    public global::Qyl.Common.TokenCount TotalOutputTokens { get; init; }

    /// <summary>Total cost in USD</summary>
    [JsonPropertyName("totalCostUsd")]
    public global::Qyl.Common.CostUsd TotalCostUsd { get; init; }

    /// <summary>Primary service name</summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    /// <summary>Primary GenAI provider</summary>
    [JsonPropertyName("genAiSystem")]
    public string? GenAiSystem { get; init; }

    /// <summary>Primary model used</summary>
    [JsonPropertyName("genAiModel")]
    public string? GenAiModel { get; init; }

}

/// <summary>OpenTelemetry span record for storage and query</summary>
public sealed record SpanRecord
{
    /// <summary>Unique span identifier</summary>
    [JsonPropertyName("spanId")]
    public global::Qyl.Common.SpanId SpanId { get; init; }

    /// <summary>Trace identifier</summary>
    [JsonPropertyName("traceId")]
    public global::Qyl.Common.TraceId TraceId { get; init; }

    /// <summary>Parent span identifier (null for root spans)</summary>
    [JsonPropertyName("parentSpanId")]
    public global::Qyl.Common.SpanId? ParentSpanId { get; init; }

    /// <summary>Session identifier for grouping related traces</summary>
    [JsonPropertyName("sessionId")]
    public global::Qyl.Common.SessionId? SessionId { get; init; }

    /// <summary>Human-readable span name</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>Span kind</summary>
    [JsonPropertyName("kind")]
    public global::Qyl.Enums.SpanKind Kind { get; init; }

    /// <summary>Start timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("startTimeUnixNano")]
    public global::Qyl.Common.UnixNano StartTimeUnixNano { get; init; }

    /// <summary>End timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("endTimeUnixNano")]
    public global::Qyl.Common.UnixNano EndTimeUnixNano { get; init; }

    /// <summary>Duration in nanoseconds</summary>
    [JsonPropertyName("durationNs")]
    public global::Qyl.Common.DurationNs DurationNs { get; init; }

    /// <summary>Span status code</summary>
    [JsonPropertyName("statusCode")]
    public global::Qyl.Enums.StatusCode StatusCode { get; init; }

    /// <summary>Status message (only for ERROR status)</summary>
    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; init; }

    /// <summary>Service name from resource attributes</summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    /// <summary>GenAI provider/system (e.g., openai, anthropic)</summary>
    [JsonPropertyName("genAiSystem")]
    public string? GenAiSystem { get; init; }

    /// <summary>Requested model name</summary>
    [JsonPropertyName("genAiRequestModel")]
    public string? GenAiRequestModel { get; init; }

    /// <summary>Actual response model name</summary>
    [JsonPropertyName("genAiResponseModel")]
    public string? GenAiResponseModel { get; init; }

    /// <summary>Input/prompt tokens</summary>
    [JsonPropertyName("genAiInputTokens")]
    public global::Qyl.Common.TokenCount? GenAiInputTokens { get; init; }

    /// <summary>Output/completion tokens</summary>
    [JsonPropertyName("genAiOutputTokens")]
    public global::Qyl.Common.TokenCount? GenAiOutputTokens { get; init; }

    /// <summary>Request temperature</summary>
    [JsonPropertyName("genAiTemperature")]
    public global::Qyl.Common.Temperature? GenAiTemperature { get; init; }

    /// <summary>Response finish reason</summary>
    [JsonPropertyName("genAiStopReason")]
    public string? GenAiStopReason { get; init; }

    /// <summary>Tool name for tool calls</summary>
    [JsonPropertyName("genAiToolName")]
    public string? GenAiToolName { get; init; }

    /// <summary>Tool call ID</summary>
    [JsonPropertyName("genAiToolCallId")]
    public string? GenAiToolCallId { get; init; }

    /// <summary>Estimated cost in USD</summary>
    [JsonPropertyName("genAiCostUsd")]
    public global::Qyl.Common.CostUsd? GenAiCostUsd { get; init; }

    /// <summary>All span attributes as JSON</summary>
    [JsonPropertyName("attributesJson")]
    public string? AttributesJson { get; init; }

    /// <summary>Resource attributes as JSON</summary>
    [JsonPropertyName("resourceJson")]
    public string? ResourceJson { get; init; }

    /// <summary>Row creation timestamp</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; init; }

}

/// <summary>Trace tree node for hierarchical span display</summary>
public sealed record TraceNode
{
    /// <summary>Span data</summary>
    [JsonPropertyName("span")]
    public global::Qyl.Models.SpanRecord Span { get; init; }

    /// <summary>Child spans</summary>
    [JsonPropertyName("children")]
    public IReadOnlyList<global::Qyl.Models.TraceNode> Children { get; init; }

    /// <summary>Depth in tree (0 = root)</summary>
    [JsonPropertyName("depth")]
    public int Depth { get; init; }

}

