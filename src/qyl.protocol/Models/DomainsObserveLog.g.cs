// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9055350+00:00
//     Models for Qyl.Domains.Observe.Log
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Observe.Log;

/// <summary>Attribute filter</summary>
public sealed record AttributeFilter
{
    /// <summary>Attribute key</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>Filter operator</summary>
    [JsonPropertyName("operator")]
    public required global::Qyl.Domains.Observe.Log.FilterOperator Operator { get; init; }

    /// <summary>Filter value</summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }

}

/// <summary>Log aggregation request</summary>
public sealed record LogAggregation
{
    /// <summary>Group by fields</summary>
    [JsonPropertyName("group_by")]
    public required IReadOnlyList<string> GroupBy { get; init; }

    /// <summary>Aggregation function</summary>
    [JsonPropertyName("function")]
    public required global::Qyl.Domains.Observe.Log.AggregationFunction Function { get; init; }

    /// <summary>Field to aggregate (for non-count)</summary>
    [JsonPropertyName("field")]
    public string? Field { get; init; }

    /// <summary>Time bucket (for time series)</summary>
    [JsonPropertyName("time_bucket")]
    public global::Qyl.Domains.Observe.Log.TimeBucket? TimeBucket { get; init; }

    /// <summary>Top N results</summary>
    [JsonPropertyName("top_n")]
    public int? TopN { get; init; }

}

/// <summary>Detected log pattern</summary>
public sealed record LogPattern
{
    /// <summary>Pattern ID</summary>
    [JsonPropertyName("pattern_id")]
    public required string PatternId { get; init; }

    /// <summary>Pattern template</summary>
    [JsonPropertyName("template")]
    public required string Template { get; init; }

    /// <summary>Sample log message</summary>
    [JsonPropertyName("sample")]
    public required string Sample { get; init; }

    /// <summary>Occurrence count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>First seen</summary>
    [JsonPropertyName("first_seen")]
    public required DateTimeOffset FirstSeen { get; init; }

    /// <summary>Last seen</summary>
    [JsonPropertyName("last_seen")]
    public required DateTimeOffset LastSeen { get; init; }

    /// <summary>Trend</summary>
    [JsonPropertyName("trend")]
    public required global::Qyl.Domains.Observe.Log.LogPatternTrend Trend { get; init; }

    /// <summary>Severity distribution</summary>
    [JsonPropertyName("severity_distribution")]
    public IReadOnlyList<global::Qyl.Domains.Observe.Log.LogSeverityStats>? SeverityDistribution { get; init; }

}

/// <summary>Log search query</summary>
public sealed record LogQuery
{
    /// <summary>Free text search</summary>
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    /// <summary>Severity filter</summary>
    [JsonPropertyName("severity_min")]
    public global::Qyl.OTel.Enums.SeverityNumber? SeverityMin { get; init; }

    /// <summary>Service name filter</summary>
    [JsonPropertyName("service_name")]
    public string? ServiceName { get; init; }

    /// <summary>Trace ID filter</summary>
    [JsonPropertyName("trace_id")]
    public global::Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Span ID filter</summary>
    [JsonPropertyName("span_id")]
    public global::Qyl.Common.SpanId? SpanId { get; init; }

    /// <summary>Time range start</summary>
    [JsonPropertyName("time_start")]
    public DateTimeOffset? TimeStart { get; init; }

    /// <summary>Time range end</summary>
    [JsonPropertyName("time_end")]
    public DateTimeOffset? TimeEnd { get; init; }

    /// <summary>Attribute filters</summary>
    [JsonPropertyName("attribute_filters")]
    public IReadOnlyList<global::Qyl.Domains.Observe.Log.AttributeFilter>? AttributeFilters { get; init; }

    /// <summary>Limit</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>Order by</summary>
    [JsonPropertyName("order_by")]
    public global::Qyl.Domains.Observe.Log.LogOrderBy? OrderBy { get; init; }

}

/// <summary>Log stats by severity</summary>
public sealed record LogSeverityStats
{
    /// <summary>Severity number</summary>
    [JsonPropertyName("severity")]
    public required global::Qyl.OTel.Enums.SeverityNumber Severity { get; init; }

    /// <summary>Severity text</summary>
    [JsonPropertyName("severity_text")]
    public required string SeverityText { get; init; }

    /// <summary>Count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required global::Qyl.Common.Percentage Percentage { get; init; }

}

