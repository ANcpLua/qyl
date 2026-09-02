using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using ProtoExponentialHistogramDataPoint = OpenTelemetry.Proto.Metrics.V1.ExponentialHistogramDataPoint;
using ProtoHistogramDataPoint = OpenTelemetry.Proto.Metrics.V1.HistogramDataPoint;
using ProtoKeyValue = OpenTelemetry.Proto.Common.V1.KeyValue;
using ProtoMetric = OpenTelemetry.Proto.Metrics.V1.Metric;
using ProtoNumberDataPoint = OpenTelemetry.Proto.Metrics.V1.NumberDataPoint;
using ProtoTemporality = OpenTelemetry.Proto.Metrics.V1.AggregationTemporality;

namespace Qyl.Collector.Ingestion;

internal static partial class OtlpConverter
{
    /// <summary>
    /// The OTLP data point flag marking a point that exists only to say "the stream is alive
    /// but produced no value". Its value field is meaningless and must not be read.
    /// </summary>
    private const uint NoRecordedValueFlag = 1;

    /// <summary>
    /// An exponential histogram is materialized into explicit buckets, so a pathological
    /// scale/offset combination would otherwise be free to allocate an unbounded bound
    /// vector. OTLP's own explicit histograms are far below this in practice.
    /// </summary>
    private const int MaxMaterializedBuckets = 4096;

    public static MetricIngestionBatch ConvertMetrics(ExportMetricsServiceRequest request)
    {
        var points = new List<MetricPointIngestionRecord>();
        var rejected = new MetricRejectionLog();

        foreach (var resourceMetrics in request.ResourceMetrics)
        {
            var serviceName = ExtractServiceNameFromProto(resourceMetrics.Resource);
            var projectIdHint = ExtractProjectIdHintFromProto(resourceMetrics.Resource);
            var resource = ExtractResourceProjection(resourceMetrics.Resource);

            foreach (var scopeMetrics in resourceMetrics.ScopeMetrics)
            {
                var schemaUrl = NullIfEmpty(scopeMetrics.SchemaUrl) ??
                                NullIfEmpty(resourceMetrics.SchemaUrl);

                foreach (var metric in scopeMetrics.Metrics)
                {
                    AppendMetric(
                        points,
                        rejected,
                        metric,
                        projectIdHint,
                        serviceName,
                        schemaUrl,
                        resource.Attributes,
                        resource.EntityRefs);
                }
            }
        }

        return new MetricIngestionBatch(points, rejected.DataPoints, rejected.Describe());
    }

    /// <summary>Accumulates the OTLP partial-success accounting for one export request.</summary>
    private sealed class MetricRejectionLog
    {
        private readonly SortedSet<string> _summaryMetrics = new(StringComparer.Ordinal);

        public long DataPoints { get; private set; }

        public void RejectSummary(string metricName, int dataPoints)
        {
            _summaryMetrics.Add(metricName);
            DataPoints += dataPoints;
        }

        public string? Describe() =>
            _summaryMetrics.Count is 0
                ? null
                : "qyl does not store OTLP summary metrics: their pre-computed quantiles cannot " +
                  "be re-aggregated over a time window or merged across series. Rejected: " +
                  string.Join(", ", _summaryMetrics);
    }

    private static void AppendMetric(
        List<MetricPointIngestionRecord> points,
        MetricRejectionLog rejected,
        ProtoMetric metric,
        string? projectIdHint,
        string serviceName,
        string? schemaUrl,
        Dictionary<string, OtlpAttributeValue> resourceAttributes,
        IReadOnlyList<ResourceEntityRefIngestionRecord> resourceEntityRefs)
    {
        if (string.IsNullOrWhiteSpace(metric.Name))
            throw new InvalidDataException("OTLP metric name must not be empty.");

        var stream = new MetricStreamMetadata(
            projectIdHint,
            metric.Name,
            NullIfEmpty(metric.Unit),
            NullIfEmpty(metric.Description),
            serviceName,
            schemaUrl,
            resourceAttributes,
            resourceEntityRefs);

        switch (metric.DataCase)
        {
            case ProtoMetric.DataOneofCase.Gauge:
                foreach (var point in metric.Gauge.DataPoints)
                {
                    points.Add(CreateNumberPoint(
                        stream,
                        point,
                        MetricKind.Gauge,
                        MetricTemporality.Unspecified,
                        isMonotonic: false));
                }

                break;

            case ProtoMetric.DataOneofCase.Sum:
                foreach (var point in metric.Sum.DataPoints)
                {
                    points.Add(CreateNumberPoint(
                        stream,
                        point,
                        MetricKind.Sum,
                        ConvertTemporality(metric.Sum.AggregationTemporality),
                        metric.Sum.IsMonotonic));
                }

                break;

            case ProtoMetric.DataOneofCase.Histogram:
                foreach (var point in metric.Histogram.DataPoints)
                {
                    points.Add(CreateHistogramPoint(
                        stream,
                        point,
                        ConvertTemporality(metric.Histogram.AggregationTemporality)));
                }

                break;

            case ProtoMetric.DataOneofCase.ExponentialHistogram:
                foreach (var point in metric.ExponentialHistogram.DataPoints)
                {
                    points.Add(CreateExponentialHistogramPoint(
                        stream,
                        point,
                        ConvertTemporality(metric.ExponentialHistogram.AggregationTemporality)));
                }

                break;

            // A summary carries pre-computed quantiles and no buckets, so it can be neither
            // re-aggregated over a window nor merged across series — every question the read
            // API answers would be unanswerable for it. Report it as an OTLP partial success
            // naming the instrument rather than failing the export the good metrics rode in on.
            case ProtoMetric.DataOneofCase.Summary:
                rejected.RejectSummary(metric.Name, metric.Summary.DataPoints.Count);
                break;

            case ProtoMetric.DataOneofCase.None:
                throw new InvalidDataException(
                    $"OTLP metric '{metric.Name}' carries no data point set.");

            default:
                throw new InvalidDataException(
                    $"OTLP metric '{metric.Name}' carries an unknown data point set.");
        }
    }

    private static MetricPointIngestionRecord CreateNumberPoint(
        MetricStreamMetadata stream,
        ProtoNumberDataPoint point,
        MetricKind kind,
        MetricTemporality temporality,
        bool isMonotonic)
    {
        var noValue = (point.Flags & NoRecordedValueFlag) is not 0;
        double? value = noValue
            ? null
            : point.ValueCase switch
            {
                ProtoNumberDataPoint.ValueOneofCase.AsDouble => point.AsDouble,
                ProtoNumberDataPoint.ValueOneofCase.AsInt => point.AsInt,
                _ => null
            };

        return CreateRecord(stream, point.Attributes, kind, temporality, isMonotonic) with
        {
            TimeUnixNano = point.TimeUnixNano,
            StartTimeUnixNano = NullIfZero(point.StartTimeUnixNano),
            Value = value
        };
    }

    private static MetricPointIngestionRecord CreateHistogramPoint(
        MetricStreamMetadata stream,
        ProtoHistogramDataPoint point,
        MetricTemporality temporality)
    {
        var noValue = (point.Flags & NoRecordedValueFlag) is not 0;
        if (point.ExplicitBounds.Count + 1 != point.BucketCounts.Count && point.BucketCounts.Count is not 0)
        {
            throw new InvalidDataException(
                $"OTLP histogram '{stream.MetricName}' must carry exactly one more bucket count " +
                $"than explicit bound; got {point.BucketCounts.Count} counts for " +
                $"{point.ExplicitBounds.Count} bounds.");
        }

        var bounds = ValidateAscendingBounds(stream.MetricName, point.ExplicitBounds);

        return CreateRecord(
                   stream,
                   point.Attributes,
                   MetricKind.Histogram,
                   temporality,
                   isMonotonic: false) with
               {
                   TimeUnixNano = point.TimeUnixNano,
                   StartTimeUnixNano = NullIfZero(point.StartTimeUnixNano),
                   Count = noValue ? null : point.Count,
                   Sum = noValue || !point.HasSum ? null : point.Sum,
                   Min = noValue || !point.HasMin ? null : point.Min,
                   Max = noValue || !point.HasMax ? null : point.Max,
                   BucketBounds = noValue || point.BucketCounts.Count is 0 ? null : bounds,
                   BucketCounts = noValue || point.BucketCounts.Count is 0 ? null : [.. point.BucketCounts]
               };
    }

    /// <summary>
    /// Materializes an exponential histogram into the same ascending explicit-bound vector an
    /// OTLP histogram uses, so the storage and query paths carry exactly one histogram shape.
    /// Buckets run negative-most first, then the zero bucket, then the positive buckets.
    /// </summary>
    private static MetricPointIngestionRecord CreateExponentialHistogramPoint(
        MetricStreamMetadata stream,
        ProtoExponentialHistogramDataPoint point,
        MetricTemporality temporality)
    {
        var noValue = (point.Flags & NoRecordedValueFlag) is not 0;
        List<double>? bounds = null;
        List<ulong>? counts = null;

        if (!noValue)
        {
            var populated = (point.Positive?.BucketCounts.Count ?? 0) +
                            (point.Negative?.BucketCounts.Count ?? 0) +
                            (point.ZeroCount > 0 ? 1 : 0);
            if (populated > MaxMaterializedBuckets)
            {
                throw new InvalidDataException(
                    $"OTLP exponential histogram '{stream.MetricName}' spans {populated} populated " +
                    $"buckets, above the {MaxMaterializedBuckets} qyl materializes.");
            }

            // base = 2^(2^-scale): bucket i covers (base^i, base^(i+1)].
            var logBase = Math.ScaleB(Math.Log(2), -point.Scale);
            bounds = new List<double>(populated);
            counts = new List<ulong>(populated + 1);

            if (point.Negative is { BucketCounts.Count: > 0 } negative)
            {
                // Ascending order over negative values is descending order over bucket index.
                for (var i = negative.BucketCounts.Count - 1; i >= 0; i--)
                {
                    bounds.Add(-Math.Exp(logBase * (negative.Offset + i)));
                    counts.Add(negative.BucketCounts[i]);
                }
            }

            if (point.ZeroCount > 0)
            {
                bounds.Add(point.ZeroThreshold);
                counts.Add(point.ZeroCount);
            }

            if (point.Positive is { BucketCounts.Count: > 0 } positive)
            {
                for (var i = 0; i < positive.BucketCounts.Count; i++)
                {
                    bounds.Add(Math.Exp(logBase * (positive.Offset + i + 1)));
                    counts.Add(positive.BucketCounts[i]);
                }
            }

            // The trailing count is the implicit (last bound, +infinity) bucket, which the
            // exponential encoding never populates: its highest bucket has a finite bound.
            counts.Add(0);

            if (bounds.Count is 0)
            {
                bounds = null;
                counts = null;
            }
        }

        return CreateRecord(
                   stream,
                   point.Attributes,
                   MetricKind.ExponentialHistogram,
                   temporality,
                   isMonotonic: false) with
               {
                   TimeUnixNano = point.TimeUnixNano,
                   StartTimeUnixNano = NullIfZero(point.StartTimeUnixNano),
                   Count = noValue ? null : point.Count,
                   Sum = noValue || !point.HasSum ? null : point.Sum,
                   Min = noValue || !point.HasMin ? null : point.Min,
                   Max = noValue || !point.HasMax ? null : point.Max,
                   BucketBounds = bounds,
                   BucketCounts = counts
               };
    }

    private static MetricPointIngestionRecord CreateRecord(
        MetricStreamMetadata stream,
        RepeatedField<ProtoKeyValue> attributes,
        MetricKind kind,
        MetricTemporality temporality,
        bool isMonotonic) =>
        new()
        {
            ProjectIdHint = stream.ProjectIdHint,
            MetricName = stream.MetricName,
            Kind = kind,
            Temporality = temporality,
            IsMonotonic = isMonotonic,
            Unit = stream.Unit,
            Description = stream.Description,
            ServiceName = stream.ServiceName,
            SchemaUrl = stream.SchemaUrl,
            Attributes = ExtractMetricAttributes(attributes),
            ResourceAttributes = stream.ResourceAttributes,
            ResourceEntityRefs = stream.ResourceEntityRefs,
            TimeUnixNano = 0
        };

    private static Dictionary<string, OtlpAttributeValue> ExtractMetricAttributes(
        RepeatedField<ProtoKeyValue> attributes)
    {
        var dict = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        foreach (var attr in attributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;
            var renamed = DeprecatedAttributeNormalizer.TryNormalize(attr.Key, out var key);
            if (!AttributeKeySets.IsSafeMetricAttribute(key)) continue;

            SetNormalizedAttribute(dict, key, ConvertProtoAnyValue(attr.Value), renamed);
        }

        return dict;
    }

    private static IReadOnlyList<double> ValidateAscendingBounds(
        string metricName,
        RepeatedField<double> bounds)
    {
        var previous = double.NegativeInfinity;
        foreach (var bound in bounds)
        {
            if (double.IsNaN(bound) || bound <= previous)
            {
                throw new InvalidDataException(
                    $"OTLP histogram '{metricName}' explicit bounds must be strictly ascending.");
            }

            previous = bound;
        }

        return [.. bounds];
    }

    private static MetricTemporality ConvertTemporality(ProtoTemporality temporality) => temporality switch
    {
        ProtoTemporality.Delta => MetricTemporality.Delta,
        ProtoTemporality.Cumulative => MetricTemporality.Cumulative,
        _ => MetricTemporality.Unspecified
    };

    private static ulong? NullIfZero(ulong value) => value is 0 ? null : value;

    private readonly record struct MetricStreamMetadata(
        string? ProjectIdHint,
        string MetricName,
        string? Unit,
        string? Description,
        string ServiceName,
        string? SchemaUrl,
        Dictionary<string, OtlpAttributeValue> ResourceAttributes,
        IReadOnlyList<ResourceEntityRefIngestionRecord> ResourceEntityRefs);
}

