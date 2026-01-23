// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9054880+00:00
//     Models for Qyl.Domains.Observe.Exceptions
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Observe.Exceptions;

/// <summary>Enriched exception with parsed stack trace</summary>
public sealed record EnrichedException
{
    /// <summary>Exception type/class name</summary>
    [JsonPropertyName("exception_type")]
    public required string ExceptionType { get; init; }

    /// <summary>Exception message</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Parsed stack trace</summary>
    [JsonPropertyName("stack_trace")]
    public global::Qyl.Domains.AI.Code.StackTrace? StackTrace { get; init; }

    /// <summary>Exception cause/inner exception</summary>
    [JsonPropertyName("cause")]
    public global::Qyl.Domains.Observe.Exceptions.EnrichedException? Cause { get; init; }

    /// <summary>Additional exception data</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? Data { get; init; }

    /// <summary>Exception fingerprint (for grouping)</summary>
    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; init; }

    /// <summary>First occurrence timestamp</summary>
    [JsonPropertyName("first_seen")]
    public DateTimeOffset? FirstSeen { get; init; }

    /// <summary>Last occurrence timestamp</summary>
    [JsonPropertyName("last_seen")]
    public DateTimeOffset? LastSeen { get; init; }

    /// <summary>Occurrence count</summary>
    [JsonPropertyName("occurrence_count")]
    public global::Qyl.Common.Count? OccurrenceCount { get; init; }

    /// <summary>Affected users count</summary>
    [JsonPropertyName("affected_users")]
    public global::Qyl.Common.Count? AffectedUsers { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public global::Qyl.Domains.Observe.Exceptions.ExceptionStatus? Status { get; init; }

}

/// <summary>Exception event following OTel spec</summary>
public sealed record ExceptionEvent
{
    /// <summary>Event name (always 'exception')</summary>
    [JsonPropertyName("event.name")]
    public required string EventName { get; init; }

    /// <summary>Exception type/class name</summary>
    [JsonPropertyName("exception.type")]
    public required string ExceptionType { get; init; }

    /// <summary>Exception message</summary>
    [JsonPropertyName("exception.message")]
    public required string ExceptionMessage { get; init; }

    /// <summary>Exception stacktrace</summary>
    [JsonPropertyName("exception.stacktrace")]
    public string? ExceptionStacktrace { get; init; }

    /// <summary>Whether the exception escaped</summary>
    [JsonPropertyName("exception.escaped")]
    public required bool ExceptionEscaped { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Associated trace ID</summary>
    [JsonPropertyName("trace_id")]
    public global::Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Associated span ID</summary>
    [JsonPropertyName("span_id")]
    public global::Qyl.Common.SpanId? SpanId { get; init; }

}

/// <summary>Exception stats by service</summary>
public sealed record ExceptionServiceStats
{
    /// <summary>Service name</summary>
    [JsonPropertyName("service_name")]
    public required string ServiceName { get; init; }

    /// <summary>Exception count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>Exception rate (per minute)</summary>
    [JsonPropertyName("rate_per_minute")]
    public required double RatePerMinute { get; init; }

}

/// <summary>Exception statistics</summary>
public sealed record ExceptionStats
{
    /// <summary>Total exception count</summary>
    [JsonPropertyName("total_count")]
    public required global::Qyl.Common.Count TotalCount { get; init; }

    /// <summary>Unique exception types</summary>
    [JsonPropertyName("unique_types")]
    public required int UniqueTypes { get; init; }

    /// <summary>Exceptions by type</summary>
    [JsonPropertyName("by_type")]
    public required IReadOnlyList<global::Qyl.Domains.Observe.Exceptions.ExceptionTypeStats> ByType { get; init; }

    /// <summary>Most affected services</summary>
    [JsonPropertyName("by_service")]
    public IReadOnlyList<global::Qyl.Domains.Observe.Exceptions.ExceptionServiceStats>? ByService { get; init; }

    /// <summary>Exception trend (up/down/stable)</summary>
    [JsonPropertyName("trend")]
    public global::Qyl.Domains.Observe.Exceptions.ExceptionTrend? Trend { get; init; }

}

/// <summary>Exception stats by type</summary>
public sealed record ExceptionTypeStats
{
    /// <summary>Exception type</summary>
    [JsonPropertyName("exception_type")]
    public required string ExceptionType { get; init; }

    /// <summary>Count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required global::Qyl.Common.Percentage Percentage { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.Observe.Exceptions.ExceptionStatus Status { get; init; }

}

