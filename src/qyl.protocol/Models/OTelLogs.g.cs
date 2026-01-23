// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9057090+00:00
//     Models for Qyl.OTel.Logs
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.OTel.Logs;

/// <summary>Log body content - can be string, structured, or bytes</summary>
public sealed record LogBody
{
}

/// <summary>Array log body</summary>
public sealed record LogBodyArray
{
    /// <summary>Array of values</summary>
    [JsonPropertyName("array_value")]
    public required IReadOnlyList<global::System.Text.Json.Nodes.JsonNode> ArrayValue { get; init; }

}

/// <summary>Binary log body</summary>
public sealed record LogBodyBytes
{
    /// <summary>Binary value (base64 encoded)</summary>
    [JsonPropertyName("bytes_value")]
    public required string BytesValue { get; init; }

}

/// <summary>Structured key-value log body</summary>
public sealed record LogBodyKvList
{
    /// <summary>Key-value pairs</summary>
    [JsonPropertyName("kv_list_value")]
    public required IReadOnlyList<global::Qyl.Common.Attribute> KvListValue { get; init; }

}

/// <summary>String log body</summary>
public sealed record LogBodyString
{
    /// <summary>String value</summary>
    [JsonPropertyName("string_value")]
    public required string StringValue { get; init; }

}

/// <summary>Log count by dimension</summary>
public sealed record LogCountByDimension
{
    /// <summary>Dimension value</summary>
    [JsonPropertyName("dimension")]
    public required string Dimension { get; init; }

    /// <summary>Log count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>Error count for this dimension</summary>
    [JsonPropertyName("error_count")]
    public required global::Qyl.Common.Count ErrorCount { get; init; }

}

/// <summary>Log count by severity level</summary>
public sealed record LogCountBySeverity
{
    /// <summary>Severity level</summary>
    [JsonPropertyName("severity")]
    public required global::Qyl.OTel.Enums.SeverityText Severity { get; init; }

    /// <summary>Log count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required global::Qyl.Common.Percentage Percentage { get; init; }

}

/// <summary>OpenTelemetry Log Record</summary>
public sealed record LogRecord
{
    /// <summary>Timestamp when the event occurred (nanoseconds since epoch)</summary>
    [JsonPropertyName("time_unix_nano")]
    public required long TimeUnixNano { get; init; }

    /// <summary>Timestamp when the log was observed/collected (nanoseconds since epoch)</summary>
    [JsonPropertyName("observed_time_unix_nano")]
    public required long ObservedTimeUnixNano { get; init; }

    /// <summary>Severity number (1-24)</summary>
    [JsonPropertyName("severity_number")]
    public required global::Qyl.OTel.Enums.SeverityNumber SeverityNumber { get; init; }

    /// <summary>Severity text (DEBUG, INFO, WARN, ERROR, etc.)</summary>
    [JsonPropertyName("severity_text")]
    public global::Qyl.OTel.Enums.SeverityText? SeverityText { get; init; }

    /// <summary>Log body - the main content</summary>
    [JsonPropertyName("body")]
    public required global::Qyl.OTel.Logs.LogBody Body { get; init; }

    /// <summary>Log attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public global::Qyl.Common.Count? DroppedAttributesCount { get; init; }

    /// <summary>Flags (trace flags)</summary>
    [JsonPropertyName("flags")]
    public int? Flags { get; init; }

    /// <summary>Associated trace ID</summary>
    [JsonPropertyName("trace_id")]
    public global::Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Associated span ID</summary>
    [JsonPropertyName("span_id")]
    public global::Qyl.Common.SpanId? SpanId { get; init; }

    /// <summary>Resource describing the entity that produced this log</summary>
    [JsonPropertyName("resource")]
    public required global::Qyl.OTel.Resource.Resource Resource { get; init; }

    /// <summary>Instrumentation scope</summary>
    [JsonPropertyName("instrumentation_scope")]
    public global::Qyl.Common.InstrumentationScope? InstrumentationScope { get; init; }

}

/// <summary>Aggregated log statistics</summary>
public sealed record LogStats
{
    /// <summary>Total log count</summary>
    [JsonPropertyName("total_count")]
    public required global::Qyl.Common.Count TotalCount { get; init; }

    /// <summary>Log counts by severity</summary>
    [JsonPropertyName("by_severity")]
    public required IReadOnlyList<global::Qyl.OTel.Logs.LogCountBySeverity> BySeverity { get; init; }

    /// <summary>Log counts by service</summary>
    [JsonPropertyName("by_service")]
    public required IReadOnlyList<global::Qyl.OTel.Logs.LogCountByDimension> ByService { get; init; }

    /// <summary>Logs per second rate</summary>
    [JsonPropertyName("logs_per_second")]
    public required double LogsPerSecond { get; init; }

    /// <summary>Error log rate</summary>
    [JsonPropertyName("error_rate")]
    public required global::Qyl.Common.Ratio ErrorRate { get; init; }

}

