using Google.Protobuf;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using qyl.grpc.Models;

namespace qyl.grpc.Protocol;

public sealed class OtlpConverter
{
    public IEnumerable<SpanModel> ConvertResourceSpans(IEnumerable<ResourceSpans> resourceSpans)
    {
        foreach (var rs in resourceSpans)
        {
            var resource = ConvertResource(rs.Resource);
            foreach (var scopeSpans in rs.ScopeSpans)
            {
                foreach (var span in scopeSpans.Spans)
                {
                    yield return ConvertSpan(span, resource);
                }
            }
        }
    }

    public IEnumerable<MetricModel> ConvertResourceMetrics(IEnumerable<ResourceMetrics> resourceMetrics)
    {
        foreach (var rm in resourceMetrics)
        {
            var resource = ConvertResource(rm.Resource);
            foreach (var scopeMetrics in rm.ScopeMetrics)
            {
                foreach (var metric in scopeMetrics.Metrics)
                {
                    yield return ConvertMetric(metric, resource);
                }
            }
        }
    }

    public IEnumerable<LogModel> ConvertResourceLogs(IEnumerable<ResourceLogs> resourceLogs)
    {
        foreach (var rl in resourceLogs)
        {
            var resource = ConvertResource(rl.Resource);
            foreach (var scopeLogs in rl.ScopeLogs)
            {
                foreach (var log in scopeLogs.LogRecords)
                {
                    yield return ConvertLog(log, resource);
                }
            }
        }
    }

    private static SpanModel ConvertSpan(Span span, ResourceModel resource)
    {
        var traceId = ToHexString(span.TraceId);
        var spanId = ToHexString(span.SpanId);
        var parentSpanId = span.ParentSpanId.IsEmpty ? null : ToHexString(span.ParentSpanId);

        return new SpanModel(
            TraceId: traceId,
            SpanId: spanId,
            ParentSpanId: parentSpanId,
            Name: span.Name,
            Kind: (SpanKind)span.Kind,
            StartTime: FromUnixNanos(span.StartTimeUnixNano),
            EndTime: FromUnixNanos(span.EndTimeUnixNano),
            Status: span.Status?.Code switch
            {
                Status.Types.StatusCode.Ok => SpanStatus.Ok,
                Status.Types.StatusCode.Error => SpanStatus.Error,
                _ => SpanStatus.Unset
            },
            StatusMessage: span.Status?.Message,
            Attributes: ConvertAttributes(span.Attributes),
            Events: span.Events.Select(e => new SpanEvent(
                e.Name,
                FromUnixNanos(e.TimeUnixNano),
                ConvertAttributes(e.Attributes)
            )).ToList(),
            Links: span.Links.Select(l => new SpanLink(
                ToHexString(l.TraceId),
                ToHexString(l.SpanId),
                ConvertAttributes(l.Attributes)
            )).ToList(),
            Resource: resource
        );
    }

    private static MetricModel ConvertMetric(Metric metric, ResourceModel resource)
    {
        var (metricType, dataPoints) = metric.DataCase switch
        {
            Metric.DataOneofCase.Gauge => (MetricType.Gauge, ConvertGaugeDataPoints(metric.Gauge)),
            Metric.DataOneofCase.Sum => (MetricType.Sum, ConvertSumDataPoints(metric.Sum)),
            Metric.DataOneofCase.Histogram => (MetricType.Histogram, ConvertHistogramDataPoints(metric.Histogram)),
            Metric.DataOneofCase.ExponentialHistogram => (MetricType.ExponentialHistogram, Array.Empty<DataPointModel>()),
            Metric.DataOneofCase.Summary => (MetricType.Summary, ConvertSummaryDataPoints(metric.Summary)),
            _ => (MetricType.Gauge, Array.Empty<DataPointModel>())
        };

        return new MetricModel(
            Name: metric.Name,
            Description: metric.Description,
            Unit: metric.Unit,
            Type: metricType,
            DataPoints: dataPoints,
            Resource: resource
        );
    }

    private static LogModel ConvertLog(LogRecord log, ResourceModel resource)
    {
        var traceId = log.TraceId.IsEmpty ? null : ToHexString(log.TraceId);
        var spanId = log.SpanId.IsEmpty ? null : ToHexString(log.SpanId);

        return new LogModel(
            Timestamp: FromUnixNanos(log.TimeUnixNano),
            ObservedTimestamp: log.ObservedTimeUnixNano > 0 ? FromUnixNanos(log.ObservedTimeUnixNano) : null,
            Severity: (SeverityLevel)log.SeverityNumber,
            SeverityText: log.SeverityText,
            Body: log.Body?.StringValue,
            TraceId: traceId,
            SpanId: spanId,
            Attributes: ConvertAttributes(log.Attributes),
            Resource: resource
        );
    }

    private static ResourceModel ConvertResource(Resource? resource)
    {
        var attributes = resource is null
            ? new Dictionary<string, AttributeValue>()
            : ConvertAttributes(resource.Attributes);
        return new ResourceModel(attributes);
    }

    private static IReadOnlyDictionary<string, AttributeValue> ConvertAttributes(
        IEnumerable<KeyValue> attributes)
    {
        var dict = new Dictionary<string, AttributeValue>();
        foreach (var attr in attributes)
        {
            dict[attr.Key] = ConvertAnyValue(attr.Value);
        }
        return dict;
    }

    private static AttributeValue ConvertAnyValue(AnyValue value) => value.ValueCase switch
    {
        AnyValue.ValueOneofCase.StringValue => new StringValue(value.StringValue),
        AnyValue.ValueOneofCase.IntValue => new IntValue(value.IntValue),
        AnyValue.ValueOneofCase.DoubleValue => new DoubleValue(value.DoubleValue),
        AnyValue.ValueOneofCase.BoolValue => new BoolValue(value.BoolValue),
        AnyValue.ValueOneofCase.BytesValue => new BytesValue(value.BytesValue.ToByteArray()),
        AnyValue.ValueOneofCase.ArrayValue => new Models.ArrayValue(
            value.ArrayValue.Values.Select(ConvertAnyValue).ToList()),
        AnyValue.ValueOneofCase.KvlistValue => new MapValue(
            value.KvlistValue.Values.ToDictionary(kv => kv.Key, kv => ConvertAnyValue(kv.Value))),
        _ => new StringValue(string.Empty)
    };

    private static IReadOnlyList<DataPointModel> ConvertGaugeDataPoints(Gauge gauge) =>
        gauge.DataPoints.Select(dp => new DataPointModel(
            FromUnixNanos(dp.TimeUnixNano),
            ConvertAttributes(dp.Attributes),
            new GaugeValue(dp.AsDouble)
        )).ToList();

    private static IReadOnlyList<DataPointModel> ConvertSumDataPoints(Sum sum) =>
        sum.DataPoints.Select(dp => new DataPointModel(
            FromUnixNanos(dp.TimeUnixNano),
            ConvertAttributes(dp.Attributes),
            new SumValue(dp.AsDouble, sum.IsMonotonic)
        )).ToList();

    private static IReadOnlyList<DataPointModel> ConvertHistogramDataPoints(Histogram histogram) =>
        histogram.DataPoints.Select(dp => new DataPointModel(
            FromUnixNanos(dp.TimeUnixNano),
            ConvertAttributes(dp.Attributes),
            new HistogramValue(
                (long)dp.Count,
                dp.Sum,
                dp.HasMin ? dp.Min : null,
                dp.HasMax ? dp.Max : null,
                dp.ExplicitBounds.ToList(),
                dp.BucketCounts.Select(c => (long)c).ToList()
            )
        )).ToList();

    private static IReadOnlyList<DataPointModel> ConvertSummaryDataPoints(Summary summary) =>
        summary.DataPoints.Select(dp => new DataPointModel(
            FromUnixNanos(dp.TimeUnixNano),
            ConvertAttributes(dp.Attributes),
            new SummaryValue(
                (long)dp.Count,
                dp.Sum,
                dp.QuantileValues.Select(q => new QuantileValue(q.Quantile, q.Value)).ToList()
            )
        )).ToList();

    private static string ToHexString(ByteString bytes)
    {
        var span = bytes.Span;
        return Convert.ToHexString(span).ToLowerInvariant();
    }

    private static DateTimeOffset FromUnixNanos(ulong nanos)
    {
        var ticks = (long)(nanos / 100);
        return new DateTimeOffset(ticks + DateTimeOffset.UnixEpoch.Ticks, TimeSpan.Zero);
    }
}
