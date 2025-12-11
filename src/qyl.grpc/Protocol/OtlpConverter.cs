using Google.Protobuf;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using qyl.grpc.Models;
using ArrayValue = qyl.grpc.Models.ArrayValue;

namespace qyl.grpc.Protocol;

public sealed class OtlpConverter
{
    public IEnumerable<SpanModel> ConvertResourceSpans(IEnumerable<ResourceSpans> resourceSpans)
    {
        foreach (ResourceSpans rs in resourceSpans)
        {
            ResourceModel resource = ConvertResource(rs.Resource);
            foreach (ScopeSpans? scopeSpans in rs.ScopeSpans)
            {
                foreach (Span? span in scopeSpans.Spans)
                    yield return ConvertSpan(span, resource);
            }
        }
    }

    public IEnumerable<MetricModel> ConvertResourceMetrics(IEnumerable<ResourceMetrics> resourceMetrics)
    {
        foreach (ResourceMetrics rm in resourceMetrics)
        {
            ResourceModel resource = ConvertResource(rm.Resource);
            foreach (ScopeMetrics? scopeMetrics in rm.ScopeMetrics)
            {
                foreach (Metric? metric in scopeMetrics.Metrics)
                    yield return ConvertMetric(metric, resource);
            }
        }
    }

    public IEnumerable<LogModel> ConvertResourceLogs(IEnumerable<ResourceLogs> resourceLogs)
    {
        foreach (ResourceLogs rl in resourceLogs)
        {
            ResourceModel resource = ConvertResource(rl.Resource);
            foreach (ScopeLogs? scopeLogs in rl.ScopeLogs)
            {
                foreach (LogRecord? log in scopeLogs.LogRecords)
                    yield return ConvertLog(log, resource);
            }
        }
    }

    private static SpanModel ConvertSpan(Span span, ResourceModel resource)
    {
        string traceId = ToHexString(span.TraceId);
        string spanId = ToHexString(span.SpanId);
        string? parentSpanId = span.ParentSpanId.IsEmpty ? null : ToHexString(span.ParentSpanId);

        return new(
            traceId,
            spanId,
            parentSpanId,
            span.Name,
            (SpanKind)span.Kind,
            FromUnixNanos(span.StartTimeUnixNano),
            FromUnixNanos(span.EndTimeUnixNano),
            span.Status?.Code switch
            {
                Status.Types.StatusCode.Ok => SpanStatus.Ok,
                Status.Types.StatusCode.Error => SpanStatus.Error,
                _ => SpanStatus.Unset
            },
            span.Status?.Message,
            ConvertAttributes(span.Attributes),
            [
                .. span.Events.Select(e => new SpanEvent(
                    e.Name,
                    FromUnixNanos(e.TimeUnixNano),
                    ConvertAttributes(e.Attributes)
                ))
            ],
            [
                .. span.Links.Select(l => new SpanLink(
                    ToHexString(l.TraceId),
                    ToHexString(l.SpanId),
                    ConvertAttributes(l.Attributes)
                ))
            ],
            resource
        );
    }

    private static MetricModel ConvertMetric(Metric metric, ResourceModel resource)
    {
        (MetricType metricType, IReadOnlyList<DataPointModel> dataPoints) = metric.DataCase switch
        {
            Metric.DataOneofCase.Gauge => (MetricType.Gauge, ConvertGaugeDataPoints(metric.Gauge)),
            Metric.DataOneofCase.Sum => (MetricType.Sum, ConvertSumDataPoints(metric.Sum)),
            Metric.DataOneofCase.Histogram => (MetricType.Histogram, ConvertHistogramDataPoints(metric.Histogram)),
            Metric.DataOneofCase.ExponentialHistogram => (MetricType.ExponentialHistogram, []),
            Metric.DataOneofCase.Summary => (MetricType.Summary, ConvertSummaryDataPoints(metric.Summary)),
            _ => (MetricType.Gauge, [])
        };

        return new(
            metric.Name,
            metric.Description,
            metric.Unit,
            metricType,
            dataPoints,
            resource
        );
    }

    private static LogModel ConvertLog(LogRecord log, ResourceModel resource)
    {
        string? traceId = log.TraceId.IsEmpty ? null : ToHexString(log.TraceId);
        string? spanId = log.SpanId.IsEmpty ? null : ToHexString(log.SpanId);

        return new(
            FromUnixNanos(log.TimeUnixNano),
            log.ObservedTimeUnixNano > 0 ? FromUnixNanos(log.ObservedTimeUnixNano) : null,
            (SeverityLevel)log.SeverityNumber,
            log.SeverityText,
            log.Body?.StringValue,
            traceId,
            spanId,
            ConvertAttributes(log.Attributes),
            resource
        );
    }

    private static ResourceModel ConvertResource(Resource? resource)
    {
        IReadOnlyDictionary<string, AttributeValue> attributes = resource is null
            ? new Dictionary<string, AttributeValue>()
            : ConvertAttributes(resource.Attributes);
        return new(attributes);
    }

    private static IReadOnlyDictionary<string, AttributeValue> ConvertAttributes(
        IEnumerable<KeyValue> attributes)
    {
        var dict = new Dictionary<string, AttributeValue>();
        foreach (KeyValue attr in attributes) dict[attr.Key] = ConvertAnyValue(attr.Value);

        return dict;
    }

    private static AttributeValue ConvertAnyValue(AnyValue value) =>
        value.ValueCase switch
        {
            AnyValue.ValueOneofCase.StringValue => new StringValue(value.StringValue),
            AnyValue.ValueOneofCase.IntValue => new IntValue(value.IntValue),
            AnyValue.ValueOneofCase.DoubleValue => new DoubleValue(value.DoubleValue),
            AnyValue.ValueOneofCase.BoolValue => new BoolValue(value.BoolValue),
            AnyValue.ValueOneofCase.BytesValue => new BytesValue(value.BytesValue.ToByteArray()),
            AnyValue.ValueOneofCase.ArrayValue => new ArrayValue(
                [.. value.ArrayValue.Values.Select(ConvertAnyValue)]),
            AnyValue.ValueOneofCase.KvlistValue => new MapValue(
                value.KvlistValue.Values.ToDictionary(kv => kv.Key, kv => ConvertAnyValue(kv.Value))),
            _ => new StringValue(string.Empty)
        };

    private static IReadOnlyList<DataPointModel> ConvertGaugeDataPoints(Gauge gauge) =>
    [
        .. gauge.DataPoints.Select(dp => new DataPointModel(
            FromUnixNanos(dp.TimeUnixNano),
            ConvertAttributes(dp.Attributes),
            new GaugeValue(dp.AsDouble)
        ))
    ];

    private static IReadOnlyList<DataPointModel> ConvertSumDataPoints(Sum sum) =>
    [
        .. sum.DataPoints.Select(dp => new DataPointModel(
            FromUnixNanos(dp.TimeUnixNano),
            ConvertAttributes(dp.Attributes),
            new SumValue(dp.AsDouble, sum.IsMonotonic)
        ))
    ];

    private static IReadOnlyList<DataPointModel> ConvertHistogramDataPoints(Histogram histogram) =>
    [
        .. histogram.DataPoints.Select(dp => new DataPointModel(
            FromUnixNanos(dp.TimeUnixNano),
            ConvertAttributes(dp.Attributes),
            new HistogramValue(
                (long)dp.Count,
                dp.Sum,
                dp.HasMin ? dp.Min : null,
                dp.HasMax ? dp.Max : null,
                [.. dp.ExplicitBounds],
                [.. dp.BucketCounts.Select(c => (long)c)]
            )
        ))
    ];

    private static IReadOnlyList<DataPointModel> ConvertSummaryDataPoints(Summary summary) =>
    [
        .. summary.DataPoints.Select(dp => new DataPointModel(
            FromUnixNanos(dp.TimeUnixNano),
            ConvertAttributes(dp.Attributes),
            new SummaryValue(
                (long)dp.Count,
                dp.Sum,
                [.. dp.QuantileValues.Select(q => new QuantileValue(q.Quantile, q.Value))]
            )
        ))
    ];

    private static string ToHexString(ByteString bytes)
    {
        ReadOnlySpan<byte> span = bytes.Span;
        return Convert.ToHexString(span).ToLowerInvariant();
    }

    private static DateTimeOffset FromUnixNanos(ulong nanos)
    {
        long ticks = (long)(nanos / 100);
        return new(ticks + DateTimeOffset.UnixEpoch.Ticks, TimeSpan.Zero);
    }
}
