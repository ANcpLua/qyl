// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9059330+00:00
//     Models for Qyl.OTel.Traces
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.OTel.Traces;

/// <summary>OpenTelemetry Span representing a single operation in a distributed trace</summary>
public sealed record Span
{
    /// <summary>Unique span identifier (16 hex chars)</summary>
    [JsonPropertyName("span_id")]
    public required global::Qyl.Common.SpanId SpanId { get; init; }

    /// <summary>Trace identifier (32 hex chars)</summary>
    [JsonPropertyName("trace_id")]
    public required global::Qyl.Common.TraceId TraceId { get; init; }

    /// <summary>Parent span identifier (null for root spans)</summary>
    [JsonPropertyName("parent_span_id")]
    public global::Qyl.Common.SpanId? ParentSpanId { get; init; }

    /// <summary>W3C trace state</summary>
    [JsonPropertyName("trace_state")]
    public global::Qyl.Common.TraceState? TraceState { get; init; }

    /// <summary>Human-readable span name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Span kind</summary>
    [JsonPropertyName("kind")]
    public required global::Qyl.OTel.Enums.SpanKind Kind { get; init; }

    /// <summary>Start timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("start_time_unix_nano")]
    public required long StartTimeUnixNano { get; init; }

    /// <summary>End timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("end_time_unix_nano")]
    public required long EndTimeUnixNano { get; init; }

    /// <summary>Span attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public global::Qyl.Common.Count? DroppedAttributesCount { get; init; }

    /// <summary>Span events (logs attached to span)</summary>
    [JsonPropertyName("events")]
    public IReadOnlyList<global::Qyl.OTel.Traces.SpanEvent>? Events { get; init; }

    /// <summary>Dropped events count</summary>
    [JsonPropertyName("dropped_events_count")]
    public global::Qyl.Common.Count? DroppedEventsCount { get; init; }

    /// <summary>Links to other spans</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<global::Qyl.OTel.Traces.SpanLink>? Links { get; init; }

    /// <summary>Dropped links count</summary>
    [JsonPropertyName("dropped_links_count")]
    public global::Qyl.Common.Count? DroppedLinksCount { get; init; }

    /// <summary>Span status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.OTel.Traces.SpanStatus Status { get; init; }

    /// <summary>Span flags</summary>
    [JsonPropertyName("flags")]
    public int? Flags { get; init; }

    /// <summary>Resource describing the entity that produced this span</summary>
    [JsonPropertyName("resource")]
    public required global::Qyl.OTel.Resource.Resource Resource { get; init; }

    /// <summary>Instrumentation scope</summary>
    [JsonPropertyName("instrumentation_scope")]
    public global::Qyl.Common.InstrumentationScope? InstrumentationScope { get; init; }

}

/// <summary>Event occurring during a span's lifetime</summary>
public sealed record SpanEvent
{
    /// <summary>Event name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Event timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("time_unix_nano")]
    public required long TimeUnixNano { get; init; }

    /// <summary>Event attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public global::Qyl.Common.Count? DroppedAttributesCount { get; init; }

}

/// <summary>Link to another span (e.g., batch processing)</summary>
public sealed record SpanLink
{
    /// <summary>Linked trace ID</summary>
    [JsonPropertyName("trace_id")]
    public required global::Qyl.Common.TraceId TraceId { get; init; }

    /// <summary>Linked span ID</summary>
    [JsonPropertyName("span_id")]
    public required global::Qyl.Common.SpanId SpanId { get; init; }

    /// <summary>Trace state of the linked span</summary>
    [JsonPropertyName("trace_state")]
    public global::Qyl.Common.TraceState? TraceState { get; init; }

    /// <summary>Link attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public global::Qyl.Common.Count? DroppedAttributesCount { get; init; }

    /// <summary>Link flags</summary>
    [JsonPropertyName("flags")]
    public int? Flags { get; init; }

}

/// <summary>Span status</summary>
public sealed record SpanStatus
{
    /// <summary>Status code</summary>
    [JsonPropertyName("code")]
    public required global::Qyl.OTel.Enums.SpanStatusCode Code { get; init; }

    /// <summary>Status message (only for ERROR status)</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

}

/// <summary>Complete trace containing all related spans</summary>
public sealed record Trace
{
    /// <summary>Trace identifier</summary>
    [JsonPropertyName("trace_id")]
    public required global::Qyl.Common.TraceId TraceId { get; init; }

    /// <summary>All spans in this trace</summary>
    [JsonPropertyName("spans")]
    public required IReadOnlyList<global::Qyl.OTel.Traces.Span> Spans { get; init; }

    /// <summary>Root span of the trace</summary>
    [JsonPropertyName("root_span")]
    public global::Qyl.OTel.Traces.Span? RootSpan { get; init; }

    /// <summary>Total span count</summary>
    [JsonPropertyName("span_count")]
    public required int SpanCount { get; init; }

    /// <summary>Trace duration in nanoseconds</summary>
    [JsonPropertyName("duration_ns")]
    public required global::Qyl.Common.DurationNs DurationNs { get; init; }

    /// <summary>Trace start time</summary>
    [JsonPropertyName("start_time")]
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>Trace end time</summary>
    [JsonPropertyName("end_time")]
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>Services involved in this trace</summary>
    [JsonPropertyName("services")]
    public required IReadOnlyList<string> Services { get; init; }

    /// <summary>Whether trace contains errors</summary>
    [JsonPropertyName("has_error")]
    public required bool HasError { get; init; }

}

