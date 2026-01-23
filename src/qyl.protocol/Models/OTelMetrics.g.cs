// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9057680+00:00
//     Models for Qyl.OTel.Metrics
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.OTel.Metrics;

/// <summary>Base data point with common fields</summary>
public sealed record DataPointBase
{
    /// <summary>Start timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("start_time_unix_nano")]
    public required long StartTimeUnixNano { get; init; }

    /// <summary>End timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("time_unix_nano")]
    public required long TimeUnixNano { get; init; }

    /// <summary>Data point attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Data point flags</summary>
    [JsonPropertyName("flags")]
    public global::Qyl.OTel.Enums.DataPointFlags? Flags { get; init; }

}

/// <summary>Exemplar - sample trace linked to metric</summary>
public sealed record Exemplar
{
    /// <summary>Timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("time_unix_nano")]
    public required long TimeUnixNano { get; init; }

    /// <summary>Value as integer</summary>
    [JsonPropertyName("as_int")]
    public long? AsInt { get; init; }

    /// <summary>Value as double</summary>
    [JsonPropertyName("as_double")]
    public double? AsDouble { get; init; }

    /// <summary>Span ID of the exemplar</summary>
    [JsonPropertyName("span_id")]
    public global::Qyl.Common.SpanId? SpanId { get; init; }

    /// <summary>Trace ID of the exemplar</summary>
    [JsonPropertyName("trace_id")]
    public global::Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Filtered attributes</summary>
    [JsonPropertyName("filtered_attributes")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? FilteredAttributes { get; init; }

}

/// <summary>Exponential histogram buckets</summary>
public sealed record ExponentialBuckets
{
    /// <summary>Offset of the first bucket</summary>
    [JsonPropertyName("offset")]
    public required int Offset { get; init; }

    /// <summary>Bucket counts</summary>
    [JsonPropertyName("bucket_counts")]
    public required IReadOnlyList<int> BucketCounts { get; init; }

}

/// <summary>Exponential histogram metric - distribution with exponential bucket boundaries</summary>
public sealed record ExponentialHistogramData
{
    /// <summary>Discriminator identifying this as exponential histogram metric data</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Exponential histogram data points</summary>
    [JsonPropertyName("data_points")]
    public required IReadOnlyList<global::Qyl.OTel.Metrics.ExponentialHistogramDataPoint> DataPoints { get; init; }

    /// <summary>Aggregation temporality</summary>
    [JsonPropertyName("aggregation_temporality")]
    public required global::Qyl.OTel.Enums.AggregationTemporality AggregationTemporality { get; init; }

}

/// <summary>Exponential histogram data point</summary>
public sealed record ExponentialHistogramDataPoint
{
    /// <summary>Number of values</summary>
    [JsonPropertyName("count")]
    public required long Count { get; init; }

    /// <summary>Sum of all values</summary>
    [JsonPropertyName("sum")]
    public double? Sum { get; init; }

    /// <summary>Scale factor for bucket boundaries</summary>
    [JsonPropertyName("scale")]
    public required int Scale { get; init; }

    /// <summary>Zero count</summary>
    [JsonPropertyName("zero_count")]
    public required long ZeroCount { get; init; }

    /// <summary>Zero threshold</summary>
    [JsonPropertyName("zero_threshold")]
    public double? ZeroThreshold { get; init; }

    /// <summary>Positive bucket counts</summary>
    [JsonPropertyName("positive")]
    public required global::Qyl.OTel.Metrics.ExponentialBuckets Positive { get; init; }

    /// <summary>Negative bucket counts</summary>
    [JsonPropertyName("negative")]
    public required global::Qyl.OTel.Metrics.ExponentialBuckets Negative { get; init; }

    /// <summary>Minimum value</summary>
    [JsonPropertyName("min")]
    public double? Min { get; init; }

    /// <summary>Maximum value</summary>
    [JsonPropertyName("max")]
    public double? Max { get; init; }

    /// <summary>Exemplars for the data point</summary>
    [JsonPropertyName("exemplars")]
    public IReadOnlyList<global::Qyl.OTel.Metrics.Exemplar>? Exemplars { get; init; }

}

/// <summary>Gauge metric - instantaneous value at a point in time</summary>
public sealed record GaugeData
{
    /// <summary>Discriminator identifying this as gauge metric data</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Gauge data points</summary>
    [JsonPropertyName("data_points")]
    public required IReadOnlyList<global::Qyl.OTel.Metrics.NumberDataPoint> DataPoints { get; init; }

}

/// <summary>Histogram metric - distribution of values in buckets</summary>
public sealed record HistogramData
{
    /// <summary>Discriminator identifying this as histogram metric data</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Histogram data points</summary>
    [JsonPropertyName("data_points")]
    public required IReadOnlyList<global::Qyl.OTel.Metrics.HistogramDataPoint> DataPoints { get; init; }

    /// <summary>Aggregation temporality</summary>
    [JsonPropertyName("aggregation_temporality")]
    public required global::Qyl.OTel.Enums.AggregationTemporality AggregationTemporality { get; init; }

}

/// <summary>Histogram data point</summary>
public sealed record HistogramDataPoint
{
    /// <summary>Number of values in the histogram</summary>
    [JsonPropertyName("count")]
    public required long Count { get; init; }

    /// <summary>Sum of all values</summary>
    [JsonPropertyName("sum")]
    public double? Sum { get; init; }

    /// <summary>Bucket counts</summary>
    [JsonPropertyName("bucket_counts")]
    public required IReadOnlyList<int> BucketCounts { get; init; }

    /// <summary>Explicit bucket boundaries</summary>
    [JsonPropertyName("explicit_bounds")]
    public required IReadOnlyList<double> ExplicitBounds { get; init; }

    /// <summary>Minimum value</summary>
    [JsonPropertyName("min")]
    public double? Min { get; init; }

    /// <summary>Maximum value</summary>
    [JsonPropertyName("max")]
    public double? Max { get; init; }

    /// <summary>Exemplars for the data point</summary>
    [JsonPropertyName("exemplars")]
    public IReadOnlyList<global::Qyl.OTel.Metrics.Exemplar>? Exemplars { get; init; }

}

/// <summary>OpenTelemetry Metric containing measurement data</summary>
public sealed record Metric
{
    /// <summary>Metric name (e.g., http.server.request.duration)</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Metric description</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Metric unit (e.g., 's', 'By', '1')</summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    /// <summary>Metric data (discriminated by type)</summary>
    [JsonPropertyName("data")]
    public required global::Qyl.OTel.Metrics.MetricData Data { get; init; }

    /// <summary>Metric metadata attributes</summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? Metadata { get; init; }

    /// <summary>Resource describing the entity that produced this metric</summary>
    [JsonPropertyName("resource")]
    public required global::Qyl.OTel.Resource.Resource Resource { get; init; }

    /// <summary>Instrumentation scope</summary>
    [JsonPropertyName("instrumentation_scope")]
    public global::Qyl.Common.InstrumentationScope? InstrumentationScope { get; init; }

}

/// <summary>Metric data discriminated by type</summary>
public sealed record MetricData
{
    /// <summary>Metric type discriminator</summary>
    [JsonPropertyName("type")]
    public required global::Qyl.OTel.Enums.MetricType Type { get; init; }

}

/// <summary>Numeric data point (for Gauge and Sum)</summary>
public sealed record NumberDataPoint
{
    /// <summary>Value as integer</summary>
    [JsonPropertyName("as_int")]
    public long? AsInt { get; init; }

    /// <summary>Value as double</summary>
    [JsonPropertyName("as_double")]
    public double? AsDouble { get; init; }

    /// <summary>Exemplars for the data point</summary>
    [JsonPropertyName("exemplars")]
    public IReadOnlyList<global::Qyl.OTel.Metrics.Exemplar>? Exemplars { get; init; }

}

/// <summary>Quantile value for summary</summary>
public sealed record QuantileValue
{
    /// <summary>Quantile (0.0 to 1.0)</summary>
    [JsonPropertyName("quantile")]
    public required double Quantile { get; init; }

    /// <summary>Value at this quantile</summary>
    [JsonPropertyName("value")]
    public required double Value { get; init; }

}

/// <summary>Sum metric - cumulative or delta counter</summary>
public sealed record SumData
{
    /// <summary>Discriminator identifying this as sum metric data</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Sum data points</summary>
    [JsonPropertyName("data_points")]
    public required IReadOnlyList<global::Qyl.OTel.Metrics.NumberDataPoint> DataPoints { get; init; }

    /// <summary>Whether the sum is monotonically increasing</summary>
    [JsonPropertyName("is_monotonic")]
    public required bool IsMonotonic { get; init; }

    /// <summary>Aggregation temporality</summary>
    [JsonPropertyName("aggregation_temporality")]
    public required global::Qyl.OTel.Enums.AggregationTemporality AggregationTemporality { get; init; }

}

/// <summary>Summary metric - pre-aggregated quantile distribution</summary>
public sealed record SummaryData
{
    /// <summary>Discriminator identifying this as summary metric data</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Summary data points</summary>
    [JsonPropertyName("data_points")]
    public required IReadOnlyList<global::Qyl.OTel.Metrics.SummaryDataPoint> DataPoints { get; init; }

}

/// <summary>Summary data point</summary>
public sealed record SummaryDataPoint
{
    /// <summary>Number of values</summary>
    [JsonPropertyName("count")]
    public required long Count { get; init; }

    /// <summary>Sum of all values</summary>
    [JsonPropertyName("sum")]
    public required double Sum { get; init; }

    /// <summary>Quantile values</summary>
    [JsonPropertyName("quantile_values")]
    public required IReadOnlyList<global::Qyl.OTel.Metrics.QuantileValue> QuantileValues { get; init; }

}

