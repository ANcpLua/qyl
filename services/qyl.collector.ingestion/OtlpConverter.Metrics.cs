using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using Qyl.Collector.Ingestion.Primitives;
using CommonContract = Qyl.Api.Contracts.Common;
using MetricContract = Qyl.Api.Contracts.OTel.Metrics;
using ProtoExemplar = OpenTelemetry.Proto.Metrics.V1.Exemplar;
using ProtoMetric = OpenTelemetry.Proto.Metrics.V1.Metric;
using ProtoNumberDataPoint = OpenTelemetry.Proto.Metrics.V1.NumberDataPoint;
using ProtoTemporality = OpenTelemetry.Proto.Metrics.V1.AggregationTemporality;

namespace Qyl.Collector.Ingestion;

internal static partial class OtlpConverter
{
    public static MetricIngestionBatch ConvertMetrics(ExportMetricsServiceRequest request)
    {
        var points = new List<MetricPointIngestionRecord>();
        var missingTimestamps = new SortedSet<string>(StringComparer.Ordinal);
        long rejected = 0;
        foreach (var resourceMetrics in request.ResourceMetrics)
        {
            var serviceName = ExtractServiceNameFromProto(resourceMetrics.Resource);
            var projectId = ExtractProjectIdHintFromProto(resourceMetrics.Resource);
            var resource = ExtractResourceProjection(resourceMetrics.Resource);
            foreach (var scopeMetrics in resourceMetrics.ScopeMetrics)
            {
                var scopeAttributes = ConvertMetadataAttributes(scopeMetrics.Scope?.Attributes ?? []);
                foreach (var metric in scopeMetrics.Metrics)
                {
                    if (string.IsNullOrWhiteSpace(metric.Name))
                        throw new InvalidDataException("OTLP metric name must not be empty.");
                    var stream = new MetricStreamMetadata(projectId, metric.Name, NullIfEmpty(metric.Unit),
                        NullIfEmpty(metric.Description), serviceName,
                        NullIfEmpty(scopeMetrics.SchemaUrl) ?? NullIfEmpty(resourceMetrics.SchemaUrl),
                        resource.Attributes, resource.EntityRefs, NullIfEmpty(resourceMetrics.SchemaUrl),
                        resourceMetrics.Resource?.DroppedAttributesCount ?? 0, NullIfEmpty(scopeMetrics.Scope?.Name),
                        NullIfEmpty(scopeMetrics.Scope?.Version), NullIfEmpty(scopeMetrics.SchemaUrl), scopeAttributes,
                        scopeMetrics.Scope?.DroppedAttributesCount ?? 0,
                        ToContractAttributes(ConvertMetadataAttributes(metric.Metadata)));

                    // End time is required for every metric kind, unlike optional log event time.
                    // https://github.com/open-telemetry/opentelemetry-proto/blob/v1.11.1/opentelemetry/proto/metrics/v1/metrics.proto
                    void Add(MetricPointIngestionRecord point)
                    {
                        if (point.TimeUnixNano != 0) points.Add(point);
                        else { rejected++; missingTimestamps.Add(metric.Name); }
                    }

                    switch (metric.DataCase)
                    {
                        case ProtoMetric.DataOneofCase.Gauge:
                            foreach (var point in metric.Gauge.DataPoints)
                                Add(CreateNumberPoint(stream, point, MetricKind.Gauge, MetricTemporality.Unspecified, false));
                            break;
                        case ProtoMetric.DataOneofCase.Sum:
                            foreach (var point in metric.Sum.DataPoints)
                                Add(CreateNumberPoint(stream, point, MetricKind.Sum,
                                    ConvertTemporality(metric.Sum.AggregationTemporality), metric.Sum.IsMonotonic));
                            break;
                        case ProtoMetric.DataOneofCase.Histogram:
                            foreach (var point in metric.Histogram.DataPoints)
                            {
                                if (point.BucketCounts.Count != 0 && point.ExplicitBounds.Count + 1 != point.BucketCounts.Count)
                                    throw new InvalidDataException($"OTLP histogram '{metric.Name}' must carry one more count than explicit bound.");
                                ValidateAscendingBounds(metric.Name, point.ExplicitBounds);
                                Add(CreateRecord(stream, point.Attributes, MetricKind.Histogram,
                                    ConvertTemporality(metric.Histogram.AggregationTemporality), false,
                                    new MetricContract.MetricHistogramPointData
                                    {
                                        Count = point.Count, Sum = point.HasSum ? point.Sum : null,
                                        Min = point.HasMin ? point.Min : null, Max = point.HasMax ? point.Max : null,
                                        ExplicitBounds = point.ExplicitBounds.ToArray(), BucketCounts = point.BucketCounts.ToArray()
                                    }) with
                                {
                                    TimeUnixNano = point.TimeUnixNano, StartTimeUnixNano = NullIfZero(point.StartTimeUnixNano),
                                    Flags = point.Flags, Exemplars = ConvertExemplars(point.Exemplars)
                                });
                            }
                            break;
                        case ProtoMetric.DataOneofCase.ExponentialHistogram:
                            foreach (var point in metric.ExponentialHistogram.DataPoints)
                                Add(CreateRecord(stream, point.Attributes, MetricKind.ExponentialHistogram,
                                    ConvertTemporality(metric.ExponentialHistogram.AggregationTemporality), false,
                                    new MetricContract.MetricExponentialHistogramPointData
                                    {
                                        Count = point.Count, Sum = point.HasSum ? point.Sum : null,
                                        Min = point.HasMin ? point.Min : null, Max = point.HasMax ? point.Max : null,
                                        Scale = point.Scale, ZeroCount = point.ZeroCount, ZeroThreshold = point.ZeroThreshold,
                                        Positive = point.Positive is null ? null : new MetricContract.MetricExponentialBuckets
                                        { Offset = point.Positive.Offset, BucketCounts = point.Positive.BucketCounts.ToArray() },
                                        Negative = point.Negative is null ? null : new MetricContract.MetricExponentialBuckets
                                        { Offset = point.Negative.Offset, BucketCounts = point.Negative.BucketCounts.ToArray() }
                                    }) with
                                {
                                    TimeUnixNano = point.TimeUnixNano, StartTimeUnixNano = NullIfZero(point.StartTimeUnixNano),
                                    Flags = point.Flags, Exemplars = ConvertExemplars(point.Exemplars)
                                });
                            break;
                        case ProtoMetric.DataOneofCase.Summary:
                            foreach (var point in metric.Summary.DataPoints)
                                Add(CreateRecord(stream, point.Attributes, MetricKind.Summary, MetricTemporality.Unspecified,
                                    false, new MetricContract.MetricSummaryPointData
                                    {
                                        Count = point.Count, Sum = point.Sum,
                                        QuantileValues = point.QuantileValues.Select(static value => new MetricContract.MetricQuantileValue
                                        { Quantile = value.Quantile, Value = value.Value }).ToArray()
                                    }) with
                                {
                                    TimeUnixNano = point.TimeUnixNano, StartTimeUnixNano = NullIfZero(point.StartTimeUnixNano),
                                    Flags = point.Flags
                                });
                            break;
                        default:
                            throw new InvalidDataException($"OTLP metric '{metric.Name}' carries an unknown data point set.");
                    }
                }
            }
        }
        return new MetricIngestionBatch(points, rejected, missingTimestamps.Count == 0 ? null :
            "Metric points require a nonzero time_unix_nano. Rejected: " + string.Join(", ", missingTimestamps));
    }

    private static MetricPointIngestionRecord CreateNumberPoint(MetricStreamMetadata stream, ProtoNumberDataPoint point,
        MetricKind kind, MetricTemporality temporality, bool monotonic) =>
        CreateRecord(stream, point.Attributes, kind, temporality, monotonic, new MetricContract.MetricNumberPointData
        {
            Value = point.ValueCase switch
            {
                ProtoNumberDataPoint.ValueOneofCase.AsInt => new CommonContract.AttributeIntValue { Value = point.AsInt },
                ProtoNumberDataPoint.ValueOneofCase.AsDouble => new CommonContract.AttributeDoubleValue { Value = point.AsDouble },
                _ => null
            }
        }) with
        {
            TimeUnixNano = point.TimeUnixNano, StartTimeUnixNano = NullIfZero(point.StartTimeUnixNano),
            Flags = point.Flags, Exemplars = ConvertExemplars(point.Exemplars)
        };

    private static IReadOnlyList<MetricContract.MetricExemplar> ConvertExemplars(IEnumerable<ProtoExemplar> exemplars) =>
        exemplars.Select(static exemplar => new MetricContract.MetricExemplar
        {
            TimeUnixNano = exemplar.TimeUnixNano,
            Value = exemplar.ValueCase switch
            {
                ProtoExemplar.ValueOneofCase.AsInt => new CommonContract.AttributeIntValue { Value = exemplar.AsInt },
                ProtoExemplar.ValueOneofCase.AsDouble => new CommonContract.AttributeDoubleValue { Value = exemplar.AsDouble },
                _ => throw new InvalidDataException("OTLP metric exemplars require a numeric value.")
            },
            FilteredAttributes = ToContractAttributes(ExtractMetricAttributes(exemplar.FilteredAttributes)),
            TraceId = MetricExemplarId(exemplar.TraceId, 16, "trace_id"),
            SpanId = MetricExemplarId(exemplar.SpanId, 8, "span_id")
        }).ToArray();

    private static string? MetricExemplarId(ByteString bytes, int length, string field)
    {
        if (bytes.IsEmpty) return null;
        if (bytes.Length != length || bytes.Span.IndexOfAnyExcept((byte)0) < 0)
            throw new InvalidDataException($"OTLP exemplar {field} must be a nonzero {length}-byte identifier.");
        return Convert.ToHexStringLower(bytes.Span);
    }

    private static IReadOnlyList<CommonContract.Attribute> ToContractAttributes(
        IReadOnlyDictionary<string, OtlpAttributeValue> attributes) => attributes.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
        .Select(static pair => new CommonContract.Attribute { Key = pair.Key, Value = pair.Value.ToContract() }).ToArray();

    private static MetricPointIngestionRecord CreateRecord(MetricStreamMetadata stream,
        RepeatedField<OpenTelemetry.Proto.Common.V1.KeyValue> attributes, MetricKind kind,
        MetricTemporality temporality, bool monotonic, MetricContract.MetricPointData data) => new()
    {
        ProjectIdHint = stream.ProjectIdHint, MetricName = stream.MetricName, Kind = kind, Temporality = temporality,
        IsMonotonic = monotonic, Unit = stream.Unit, Description = stream.Description, ServiceName = stream.ServiceName,
        SchemaUrl = stream.SchemaUrl, ResourceSchemaUrl = stream.ResourceSchemaUrl,
        ResourceDroppedAttributesCount = stream.ResourceDroppedAttributesCount, ScopeName = stream.ScopeName,
        ScopeVersion = stream.ScopeVersion, ScopeSchemaUrl = stream.ScopeSchemaUrl, ScopeAttributes = stream.ScopeAttributes,
        ScopeDroppedAttributesCount = stream.ScopeDroppedAttributesCount, Attributes = ExtractMetricAttributes(attributes),
        ResourceAttributes = stream.ResourceAttributes, ResourceEntityRefs = stream.ResourceEntityRefs,
        InstrumentMetadata = stream.InstrumentMetadata, TimeUnixNano = 0, Data = data
    };

    private static Dictionary<string, OtlpAttributeValue> ExtractMetricAttributes(
        RepeatedField<OpenTelemetry.Proto.Common.V1.KeyValue> attributes)
    {
        var result = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        foreach (var attribute in attributes)
        {
            if (string.IsNullOrEmpty(attribute.Key)) continue;
            var renamed = DeprecatedAttributeNormalizer.TryNormalize(attribute.Key, out var key);
            if (!AttributeKeySets.IsSafeMetricAttribute(key)) { QylCollectorMetrics.AttributeDropped(key); continue; }
            SetNormalizedAttribute(result, key, ConvertProtoAnyValue(attribute.Value), renamed);
        }
        return result;
    }

    private static void ValidateAscendingBounds(string metricName, RepeatedField<double> bounds)
    {
        var previous = double.NegativeInfinity;
        foreach (var bound in bounds)
        {
            if (!double.IsFinite(bound) || bound <= previous)
                throw new InvalidDataException($"OTLP histogram '{metricName}' explicit bounds must be finite and strictly ascending.");
            previous = bound;
        }
    }

    private static MetricTemporality ConvertTemporality(ProtoTemporality temporality) => temporality switch
    {
        ProtoTemporality.Delta => MetricTemporality.Delta,
        ProtoTemporality.Cumulative => MetricTemporality.Cumulative,
        _ => MetricTemporality.Unspecified
    };

    private static ulong? NullIfZero(ulong value) => value == 0 ? null : value;

    private readonly record struct MetricStreamMetadata(string? ProjectIdHint, string MetricName, string? Unit,
        string? Description, string ServiceName, string? SchemaUrl,
        Dictionary<string, OtlpAttributeValue> ResourceAttributes, IReadOnlyList<ResourceEntityRefIngestionRecord> ResourceEntityRefs,
        string? ResourceSchemaUrl, uint ResourceDroppedAttributesCount, string? ScopeName, string? ScopeVersion,
        string? ScopeSchemaUrl, IReadOnlyDictionary<string, OtlpAttributeValue> ScopeAttributes,
        uint ScopeDroppedAttributesCount, IReadOnlyList<CommonContract.Attribute> InstrumentMetadata);
}
