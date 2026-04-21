// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-04-10T01:52:32.6742090+00:00
//     Models for Qyl.Storage
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Storage;

/// <summary>OpenTelemetry profile record for storage and query</summary>
public sealed record ProfileRecord
{
    /// <summary>Unique profile identifier</summary>
    [JsonPropertyName("profileId")]
    public required string ProfileId { get; init; }

    /// <summary>Correlated trace ID (from Link table)</summary>
    [JsonPropertyName("traceId")]
    public global::Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Correlated span ID (from Link table)</summary>
    [JsonPropertyName("spanId")]
    public global::Qyl.Common.SpanId? SpanId { get; init; }

    /// <summary>Session identifier for grouping related profiles</summary>
    [JsonPropertyName("sessionId")]
    public global::Qyl.Common.SessionId? SessionId { get; init; }

    /// <summary>Profile start timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("timeUnixNano")]
    public required long TimeUnixNano { get; init; }

    /// <summary>Profile duration in nanoseconds</summary>
    [JsonPropertyName("durationNano")]
    public required long DurationNano { get; init; }

    /// <summary>Number of samples in this profile</summary>
    [JsonPropertyName("sampleCount")]
    public required int SampleCount { get; init; }

    /// <summary>Sample type (e.g., cpu, alloc_objects, wall)</summary>
    [JsonPropertyName("sampleType")]
    public string? SampleType { get; init; }

    /// <summary>Sample unit (e.g., nanoseconds, bytes, count)</summary>
    [JsonPropertyName("sampleUnit")]
    public string? SampleUnit { get; init; }

    /// <summary>Original payload format</summary>
    [JsonPropertyName("originalPayloadFormat")]
    public string? OriginalPayloadFormat { get; init; }

    /// <summary>Service name from resource attributes</summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    /// <summary>Profile frame type (dotnet, jvm, cpython, etc.)</summary>
    [JsonPropertyName("profileFrameType")]
    public string? ProfileFrameType { get; init; }

    /// <summary>Profile attributes as JSON</summary>
    [JsonPropertyName("attributesJson")]
    public string? AttributesJson { get; init; }

    /// <summary>Resource attributes as JSON</summary>
    [JsonPropertyName("resourceJson")]
    public string? ResourceJson { get; init; }

    /// <summary>Full profile structure as JSON blob (denormalized for single-query access)</summary>
    [JsonPropertyName("profileDataJson")]
    public string? ProfileDataJson { get; init; }

    /// <summary>OTel semantic convention schema URL</summary>
    [JsonPropertyName("schemaUrl")]
    public string? SchemaUrl { get; init; }

    /// <summary>Row creation timestamp</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; init; }

}

/// <summary>OpenTelemetry span record for storage and query</summary>
public sealed record SpanRecord
{
    /// <summary>Unique span identifier</summary>
    [JsonPropertyName("spanId")]
    public required global::Qyl.Common.SpanId SpanId { get; init; }

    /// <summary>Trace identifier</summary>
    [JsonPropertyName("traceId")]
    public required global::Qyl.Common.TraceId TraceId { get; init; }

    /// <summary>Parent span identifier (null for root spans)</summary>
    [JsonPropertyName("parentSpanId")]
    public global::Qyl.Common.SpanId? ParentSpanId { get; init; }

    /// <summary>Session identifier for grouping related traces</summary>
    [JsonPropertyName("sessionId")]
    public global::Qyl.Common.SessionId? SessionId { get; init; }

    /// <summary>Human-readable span name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Span kind</summary>
    [JsonPropertyName("kind")]
    public required global::Qyl.OTel.Enums.SpanKind Kind { get; init; }

    /// <summary>Start timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("startTimeUnixNano")]
    public required long StartTimeUnixNano { get; init; }

    /// <summary>End timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("endTimeUnixNano")]
    public required long EndTimeUnixNano { get; init; }

    /// <summary>Duration in nanoseconds</summary>
    [JsonPropertyName("durationNs")]
    public required long DurationNs { get; init; }

    /// <summary>Span status code</summary>
    [JsonPropertyName("statusCode")]
    public required global::Qyl.OTel.Enums.SpanStatusCode StatusCode { get; init; }

    /// <summary>Status message (only for ERROR status)</summary>
    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; init; }

    /// <summary>Service name from resource attributes</summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    /// <summary>GenAI provider name (e.g., openai, anthropic) - OTel 1.40: gen_ai.provider.name</summary>
    [JsonPropertyName("genAiProviderName")]
    public string? GenAiProviderName { get; init; }

    /// <summary>Requested model name</summary>
    [JsonPropertyName("genAiRequestModel")]
    public string? GenAiRequestModel { get; init; }

    /// <summary>Actual response model name</summary>
    [JsonPropertyName("genAiResponseModel")]
    public string? GenAiResponseModel { get; init; }

    /// <summary>Input/prompt tokens</summary>
    [JsonPropertyName("genAiInputTokens")]
    public long? GenAiInputTokens { get; init; }

    /// <summary>Output/completion tokens</summary>
    [JsonPropertyName("genAiOutputTokens")]
    public long? GenAiOutputTokens { get; init; }

    /// <summary>Request temperature</summary>
    [JsonPropertyName("genAiTemperature")]
    public double? GenAiTemperature { get; init; }

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
    public double? GenAiCostUsd { get; init; }

    /// <summary>All span attributes as JSON</summary>
    [JsonPropertyName("attributesJson")]
    public string? AttributesJson { get; init; }

    /// <summary>Resource attributes as JSON</summary>
    [JsonPropertyName("resourceJson")]
    public string? ResourceJson { get; init; }

    /// <summary>W3C Baggage key-value pairs as JSON for cross-cutting concern propagation</summary>
    [JsonPropertyName("baggageJson")]
    public string? BaggageJson { get; init; }

    /// <summary>OTel semantic convention schema URL (e.g., https://opentelemetry.io/schemas/1.40.0)</summary>
    [JsonPropertyName("schemaUrl")]
    public string? SchemaUrl { get; init; }

    /// <summary>Row creation timestamp</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; init; }

}

