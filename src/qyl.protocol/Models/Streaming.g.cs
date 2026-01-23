// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9060000+00:00
//     Models for Qyl.Streaming
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Streaming;

/// <summary>Deployment stream event</summary>
public sealed record DeploymentStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Deployment event data</summary>
    [JsonPropertyName("data")]
    public required global::Qyl.Domains.Ops.Deployment.DeploymentEvent Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Exception stream event</summary>
public sealed record ExceptionStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Exception data</summary>
    [JsonPropertyName("data")]
    public required global::Qyl.Domains.Observe.Exceptions.ExceptionEvent Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Heartbeat event for connection keep-alive</summary>
public sealed record HeartbeatEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Server timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Log stream event</summary>
public sealed record LogStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Log data</summary>
    [JsonPropertyName("data")]
    public required global::Qyl.OTel.Logs.LogRecord Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Metric stream event</summary>
public sealed record MetricStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Metric data</summary>
    [JsonPropertyName("data")]
    public required global::Qyl.OTel.Metrics.Metric Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Span stream event</summary>
public sealed record SpanStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Span data</summary>
    [JsonPropertyName("data")]
    public required global::Qyl.OTel.Traces.Span Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Stream subscription request</summary>
public sealed record StreamSubscription
{
    /// <summary>Event types to subscribe to</summary>
    [JsonPropertyName("event_types")]
    public required IReadOnlyList<global::Qyl.Streaming.StreamEventType> EventTypes { get; init; }

    /// <summary>Service name filter</summary>
    [JsonPropertyName("service_name")]
    public string? ServiceName { get; init; }

    /// <summary>Trace ID filter (for specific trace)</summary>
    [JsonPropertyName("trace_id")]
    public global::Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Minimum severity for logs (1-24)</summary>
    [JsonPropertyName("min_severity")]
    public int? MinSeverity { get; init; }

    /// <summary>Attribute filters</summary>
    [JsonPropertyName("filters")]
    public object? Filters { get; init; }

    /// <summary>Sample rate (0.0-1.0)</summary>
    [JsonPropertyName("sample_rate")]
    public double? SampleRate { get; init; }

}

/// <summary>Tail-based sampling configuration for streaming</summary>
public sealed record TailSamplingConfig
{
    /// <summary>Enable tail-based sampling</summary>
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    /// <summary>Sample error traces</summary>
    [JsonPropertyName("sample_errors")]
    public required bool SampleErrors { get; init; }

    /// <summary>Sample slow traces (above threshold)</summary>
    [JsonPropertyName("sample_slow")]
    public required bool SampleSlow { get; init; }

    /// <summary>Slow trace threshold in milliseconds</summary>
    [JsonPropertyName("slow_threshold_ms")]
    public long? SlowThresholdMs { get; init; }

    /// <summary>Random sample rate for remaining traces</summary>
    [JsonPropertyName("random_rate")]
    public required double RandomRate { get; init; }

}

/// <summary>Trace stream event</summary>
public sealed record TraceStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Trace data</summary>
    [JsonPropertyName("data")]
    public required global::Qyl.OTel.Traces.Trace Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

