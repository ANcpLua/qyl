
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Collector.Trace.V1;
using ProtoAnyValue = OpenTelemetry.Proto.Common.V1.AnyValue;
using ProtoArrayValue = OpenTelemetry.Proto.Common.V1.ArrayValue;
using ProtoKeyValue = OpenTelemetry.Proto.Common.V1.KeyValue;
using ProtoKeyValueList = OpenTelemetry.Proto.Common.V1.KeyValueList;
using ProtoLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;
using ProtoExemplar = OpenTelemetry.Proto.Metrics.V1.Exemplar;
using ProtoMetric = OpenTelemetry.Proto.Metrics.V1.Metric;
using ProtoNumberDataPoint = OpenTelemetry.Proto.Metrics.V1.NumberDataPoint;
using ProtoResourceMetrics = OpenTelemetry.Proto.Metrics.V1.ResourceMetrics;
using ProtoScopeMetrics = OpenTelemetry.Proto.Metrics.V1.ScopeMetrics;
using ProtoProfile = OpenTelemetry.Proto.Profiles.V1Development.Profile;
using ProtoProfilesDictionary = OpenTelemetry.Proto.Profiles.V1Development.ProfilesDictionary;
using ProtoResource = OpenTelemetry.Proto.Resource.V1.Resource;
using ProtoSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Qyl.Collector.Ingestion;

internal static class OtlpConverter
{
    #region OTLP Trace Conversion

    public static TraceIngestionBatch ConvertTraceRequest(ExportTraceServiceRequest request)
    {
        var spans = new List<SpanIngestionRecord>();

        foreach (var resourceSpan in request.ResourceSpans)
        {
            var serviceName = ExtractServiceNameFromProto(resourceSpan.Resource);
            var projectIdHint = ExtractProjectIdHintFromProto(resourceSpan.Resource);
            var resourceAttrs = ExtractResourceAttributesFromProto(resourceSpan.Resource);
            var schemaUrl = resourceSpan.SchemaUrl;

            foreach (var scopeSpan in resourceSpan.ScopeSpans)
            {
                var effectiveSchemaUrl = !string.IsNullOrEmpty(scopeSpan.SchemaUrl)
                    ? scopeSpan.SchemaUrl
                    : schemaUrl;

                foreach (var span in scopeSpan.Spans)
                {
                    var attributes = ExtractAttributesFromProto(span.Attributes, serviceName);
                    var spanRecord =
                        CreateSpanRecordFromProto(
                            span,
                            projectIdHint,
                            serviceName,
                            attributes,
                            resourceAttrs,
                            effectiveSchemaUrl);
                    spans.Add(spanRecord);
                }
            }
        }

        return new TraceIngestionBatch(spans);
    }

    private static string ExtractServiceNameFromProto(ProtoResource? resource)
    {
        if (resource is null) return "unknown";

        foreach (var attr in resource.Attributes)
        {
            if (attr is
                {
                    Key: CollectorSemanticAttributeCatalog.ServiceName,
                    Value.ValueCase: ProtoAnyValue.ValueOneofCase.StringValue
                })
                return attr.Value.StringValue;
        }

        return "unknown";
    }

    private static string? ExtractProjectIdHintFromProto(ProtoResource? resource)
    {
        if (resource is null)
            return null;

        foreach (var attr in resource.Attributes)
        {
            if (!attr.Key.IsAny(AttributeKeySets.ProjectIdResourceKeys) ||
                attr.Value.ValueCase is not ProtoAnyValue.ValueOneofCase.StringValue ||
                string.IsNullOrWhiteSpace(attr.Value.StringValue))
            {
                continue;
            }

            return attr.Value.StringValue;
        }

        return null;
    }

    private static Dictionary<string, OtlpAttributeValue> ExtractResourceAttributesFromProto(ProtoResource? resource)
    {
        var attrs = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        if (resource is null) return attrs;

        foreach (var attr in resource.Attributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;
            var renamed = DeprecatedAttributeNormalizer.TryNormalize(attr.Key, out var key);
            if (!AttributeKeySets.IsSafeResourceAttribute(key)) continue;

            var value = ConvertProtoAnyValue(attr.Value);
            if (value is null) continue;
            if (renamed) attrs.TryAdd(key, value);
            else attrs[key] = value;
        }

        return attrs;
    }

    private static Dictionary<string, OtlpAttributeValue> ExtractAttributesFromProto(
        RepeatedField<ProtoKeyValue> protoAttributes,
        string serviceName)
    {
        var attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal)
        {
            [CollectorSemanticAttributeCatalog.ServiceName] = OtlpAttributeValue.FromString(serviceName)
        };

        foreach (var attr in protoAttributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;
            var renamed = DeprecatedAttributeNormalizer.TryNormalize(attr.Key, out var key);
            if (!AttributeKeySets.ShouldCaptureSpanAttribute(key)) continue;

            var value = ConvertProtoAnyValue(attr.Value);
            if (value is null) continue;
            if (renamed) attributes.TryAdd(key, value);
            else attributes[key] = value;
        }

        return attributes;
    }

    private static IReadOnlyDictionary<string, OtlpAttributeValue> ConvertSpanChildAttributes(
        RepeatedField<ProtoKeyValue> protoAttributes)
    {
        var attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        foreach (var attr in protoAttributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;
            var renamed = DeprecatedAttributeNormalizer.TryNormalize(attr.Key, out var key);
            if (!AttributeKeySets.ShouldCaptureSpanAttribute(key)) continue;

            var value = ConvertProtoAnyValue(attr.Value);
            if (value is null) continue;
            if (renamed) attributes.TryAdd(key, value);
            else attributes[key] = value;
        }

        return attributes;
    }

    private static IReadOnlyList<SpanEventIngest> BuildSpanEvents(ProtoSpan span)
    {
        if (span.Events.Count is 0) return [];

        var events = new List<SpanEventIngest>(span.Events.Count);
        foreach (var e in span.Events)
            events.Add(new SpanEventIngest(e.Name ?? "", e.TimeUnixNano, ConvertSpanChildAttributes(e.Attributes)));

        return events;
    }

    private static IReadOnlyList<SpanLinkIngest> BuildSpanLinks(ProtoSpan span)
    {
        if (span.Links.Count is 0) return [];

        var links = new List<SpanLinkIngest>(span.Links.Count);
        foreach (var l in span.Links)
        {
            links.Add(new SpanLinkIngest(
                RequireId(l.TraceId, 16, "link.trace_id"),
                RequireId(l.SpanId, 8, "link.span_id"),
                ConvertSpanChildAttributes(l.Attributes)));
        }

        return links;
    }

    private static OtlpAttributeValue? ConvertProtoAnyValue(ProtoAnyValue? value)
    {
        if (value is null) return null;

        return value.ValueCase switch
        {
            ProtoAnyValue.ValueOneofCase.StringValue => OtlpAttributeValue.FromString(value.StringValue),
            ProtoAnyValue.ValueOneofCase.IntValue => OtlpAttributeValue.FromInt(value.IntValue),
            ProtoAnyValue.ValueOneofCase.DoubleValue => OtlpAttributeValue.FromDouble(value.DoubleValue),
            ProtoAnyValue.ValueOneofCase.BoolValue => OtlpAttributeValue.FromBool(value.BoolValue),
            ProtoAnyValue.ValueOneofCase.BytesValue => OtlpAttributeValue.FromBytes(CopyBytes(value.BytesValue)),
            ProtoAnyValue.ValueOneofCase.ArrayValue => ConvertProtoArrayValue(value.ArrayValue),
            ProtoAnyValue.ValueOneofCase.KvlistValue => ConvertProtoKeyValueList(value.KvlistValue),
            _ => null
        };
    }

    private static OtlpAttributeValue ConvertProtoArrayValue(ProtoArrayValue value)
    {
        var items = new List<OtlpAttributeValue>(value.Values.Count);
        foreach (var item in value.Values)
        {
            if (ConvertProtoAnyValue(item) is { } converted)
                items.Add(converted);
        }

        return OtlpAttributeValue.FromArray(items);
    }

    private static OtlpAttributeValue ConvertProtoKeyValueList(ProtoKeyValueList value)
    {
        var items = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        foreach (var item in value.Values)
        {
            if (string.IsNullOrEmpty(item.Key))
                continue;

            if (ConvertProtoAnyValue(item.Value) is { } converted)
                items[item.Key] = converted;
        }

        return OtlpAttributeValue.FromKeyValueList(items);
    }

    private static SpanIngestionRecord CreateSpanRecordFromProto(
        ProtoSpan span,
        string? projectIdHint,
        string serviceName,
        Dictionary<string, OtlpAttributeValue> attributes,
        Dictionary<string, OtlpAttributeValue> resourceAttributes,
        string? schemaUrl) =>
        new()
        {
            ProjectIdHint = projectIdHint,
            SpanId = RequireId(span.SpanId, 8, "span_id"),
            TraceId = RequireId(span.TraceId, 16, "trace_id"),
            ParentSpanId = RequireIdOrAbsent(span.ParentSpanId, 8, "parent_span_id"),
            Name = span.Name ?? "unknown",
            Kind = (int)span.Kind,
            StartTimeUnixNano = span.StartTimeUnixNano,
            EndTimeUnixNano = span.EndTimeUnixNano,
            StatusCode = span.Status is not null ? (int)span.Status.Code : null,
            StatusMessage = string.IsNullOrEmpty(span.Status?.Message) ? null : span.Status.Message,
            ServiceName = serviceName,
            Attributes = attributes,
            ResourceAttributes = resourceAttributes,
            SchemaUrl = schemaUrl,
            Events = BuildSpanEvents(span),
            Links = BuildSpanLinks(span)
        };

    #endregion

    #region Shared Helpers

    // The spec fixes trace ids at 16 bytes and span ids at 8; anything else is sender corruption
    // and must reject the request cleanly (HTTP 400 / gRPC InvalidArgument), never store an
    // unjoinable id.
    private static string RequireId(ByteString value, int requiredBytes, string field)
    {
        if (value.Length != requiredBytes)
        {
            throw new InvalidDataException(
                $"OTLP field '{field}' must be {requiredBytes} bytes; got {value.Length}.");
        }

        return Convert.ToHexString(value.Span).ToLowerInvariant();
    }

    private static string? RequireIdOrAbsent(ByteString value, int requiredBytes, string field) =>
        value.Length is 0 ? null : RequireId(value, requiredBytes, field);

    private static byte[] CopyBytes(ByteString value)
    {
        var bytes = new byte[value.Length];
        value.CopyTo(bytes, 0);
        return bytes;
    }

    #endregion

    #region Logs Conversion

    public static LogIngestionBatch ConvertLogs(ExportLogsServiceRequest otlp)
    {
        var logs = new List<LogIngestionRecord>();

        foreach (var resourceLogs in otlp.ResourceLogs)
        {
            var serviceName = ExtractServiceNameFromProto(resourceLogs.Resource);
            var projectIdHint = ExtractProjectIdHintFromProto(resourceLogs.Resource);

            var resourceAttrs = ExtractResourceAttributesFromProto(resourceLogs.Resource);

            foreach (var scopeLogs in resourceLogs.ScopeLogs)
            foreach (var log in scopeLogs.LogRecords)
            {
                logs.Add(CreateLogRecord(log, projectIdHint, serviceName, resourceAttrs));
            }
        }

        return new LogIngestionBatch(logs);
    }

    private static LogIngestionRecord CreateLogRecord(
        ProtoLogRecord log,
        string? projectIdHint,
        string serviceName,
        Dictionary<string, OtlpAttributeValue> resourceAttributes)
    {
        var attributes = ExtractLogAttributes(log.Attributes);

        var severityNumber = (int)log.SeverityNumber;

        return new LogIngestionRecord
        {
            ProjectIdHint = projectIdHint,
            TraceId = RequireIdOrAbsent(log.TraceId, 16, "trace_id"),
            SpanId = RequireIdOrAbsent(log.SpanId, 8, "span_id"),
            EventName = NullIfEmpty(log.EventName),
            TimeUnixNano = log.TimeUnixNano,
            ObservedTimeUnixNano = log.ObservedTimeUnixNano > 0 ? log.ObservedTimeUnixNano : null,
            SeverityNumber = severityNumber,
            SeverityText = log.SeverityText,
            BodyText = ConvertProtoAnyValue(log.Body)?.ToStableString(),
            ServiceName = serviceName,
            Attributes = attributes,
            ResourceAttributes = resourceAttributes
        };
    }

    private static Dictionary<string, OtlpAttributeValue> ExtractLogAttributes(RepeatedField<ProtoKeyValue> attributes)
    {
        var dict = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        foreach (var attr in attributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;
            var renamed = DeprecatedAttributeNormalizer.TryNormalize(attr.Key, out var key);
            if (!AttributeKeySets.IsSafeLogAttribute(key) &&
                !key.IsAny(AttributeKeySets.SessionCorrelation))
            {
                continue;
            }

            var value = ConvertProtoAnyValue(attr.Value);
            if (value is null) continue;
            if (renamed) dict.TryAdd(key, value);
            else dict[key] = value;
        }

        return dict;
    }

    #endregion

    #region Metrics Conversion

    public static MetricIngestionBatch ConvertMetrics(ExportMetricsServiceRequest otlp)
    {
        var metrics = new List<MetricIngestionRecord>();

        foreach (var resourceMetrics in otlp.ResourceMetrics)
        {
            var serviceName = ExtractServiceNameFromProto(resourceMetrics.Resource);
            var projectIdHint = ExtractProjectIdHintFromProto(resourceMetrics.Resource);
            var resourceAttrs = ExtractResourceAttributesFromProto(resourceMetrics.Resource);

            foreach (var scopeMetrics in resourceMetrics.ScopeMetrics)
            {
                foreach (var metric in scopeMetrics.Metrics)
                {
                    AppendMetricDataPoints(
                        metrics,
                        metric,
                        projectIdHint,
                        serviceName,
                        resourceAttrs,
                        resourceMetrics,
                        scopeMetrics);
                }
            }
        }

        return new MetricIngestionBatch(metrics);
    }

    private static void AppendMetricDataPoints(
        List<MetricIngestionRecord> metrics,
        ProtoMetric metric,
        string? projectIdHint,
        string serviceName,
        Dictionary<string, OtlpAttributeValue> resourceAttributes,
        ProtoResourceMetrics resourceMetrics,
        ProtoScopeMetrics scopeMetrics)
    {
        if (string.IsNullOrWhiteSpace(metric.Name))
            throw new InvalidDataException("OTLP metric name must not be empty.");

        var metadata = ExtractMetricAttributes(metric.Metadata);
        var scopeAttributes = scopeMetrics.Scope is null
            ? new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal)
            : ExtractMetricAttributes(scopeMetrics.Scope.Attributes);

        switch (metric.DataCase)
        {
            case ProtoMetric.DataOneofCase.Gauge:
                foreach (var point in metric.Gauge.DataPoints)
                {
                    metrics.Add(CreateNumberMetricRecord(
                        metric,
                        MetricStorageTypes.Gauge,
                        point,
                        projectIdHint,
                        serviceName,
                        resourceAttributes,
                        resourceMetrics,
                        scopeMetrics,
                        metadata,
                        scopeAttributes,
                        isMonotonic: null,
                        aggregationTemporality: null));
                }

                break;
            case ProtoMetric.DataOneofCase.Sum:
                var sumTemporality = RequireAggregationTemporality(
                    (int)metric.Sum.AggregationTemporality,
                    metric.Name);
                foreach (var point in metric.Sum.DataPoints)
                {
                    metrics.Add(CreateNumberMetricRecord(
                        metric,
                        MetricStorageTypes.Sum,
                        point,
                        projectIdHint,
                        serviceName,
                        resourceAttributes,
                        resourceMetrics,
                        scopeMetrics,
                        metadata,
                        scopeAttributes,
                        metric.Sum.IsMonotonic,
                        sumTemporality));
                }

                break;
            case ProtoMetric.DataOneofCase.Histogram:
                var histogramTemporality = RequireAggregationTemporality(
                    (int)metric.Histogram.AggregationTemporality,
                    metric.Name);
                foreach (var point in metric.Histogram.DataPoints)
                {
                    ValidateExplicitHistogramBuckets(point.ExplicitBounds.Count, point.BucketCounts.Count, metric.Name);
                    metrics.Add(CreateMetricRecord(
                        metric,
                        MetricStorageTypes.Histogram,
                        point.TimeUnixNano,
                        point.StartTimeUnixNano,
                        point.Flags,
                        point.Attributes,
                        point.Exemplars,
                        projectIdHint,
                        serviceName,
                        resourceAttributes,
                        resourceMetrics,
                        scopeMetrics,
                        metadata,
                        scopeAttributes) with
                    {
                        Count = point.Count,
                        Sum = point.HasSum ? point.Sum : null,
                        Min = point.HasMin ? point.Min : null,
                        Max = point.HasMax ? point.Max : null,
                        HistogramBounds = [.. point.ExplicitBounds],
                        HistogramCounts = [.. point.BucketCounts],
                        AggregationTemporality = histogramTemporality
                    });
                }

                break;
            case ProtoMetric.DataOneofCase.ExponentialHistogram:
                var exponentialTemporality = RequireAggregationTemporality(
                    (int)metric.ExponentialHistogram.AggregationTemporality,
                    metric.Name);
                foreach (var point in metric.ExponentialHistogram.DataPoints)
                {
                    metrics.Add(CreateMetricRecord(
                        metric,
                        MetricStorageTypes.ExponentialHistogram,
                        point.TimeUnixNano,
                        point.StartTimeUnixNano,
                        point.Flags,
                        point.Attributes,
                        point.Exemplars,
                        projectIdHint,
                        serviceName,
                        resourceAttributes,
                        resourceMetrics,
                        scopeMetrics,
                        metadata,
                        scopeAttributes) with
                    {
                        Count = point.Count,
                        Sum = point.HasSum ? point.Sum : null,
                        Min = point.HasMin ? point.Min : null,
                        Max = point.HasMax ? point.Max : null,
                        ExponentialHistogramScale = point.Scale,
                        ExponentialHistogramZeroCount = point.ZeroCount,
                        ExponentialHistogramZeroThreshold = point.ZeroThreshold,
                        ExponentialHistogramPositive = ConvertExponentialBuckets(point.Positive),
                        ExponentialHistogramNegative = ConvertExponentialBuckets(point.Negative),
                        AggregationTemporality = exponentialTemporality
                    });
                }

                break;
            case ProtoMetric.DataOneofCase.Summary:
                foreach (var point in metric.Summary.DataPoints)
                {
                    ValidateSummaryQuantiles(point.QuantileValues, metric.Name);
                    metrics.Add(CreateMetricRecord(
                        metric,
                        MetricStorageTypes.Summary,
                        point.TimeUnixNano,
                        point.StartTimeUnixNano,
                        point.Flags,
                        point.Attributes,
                        exemplars: null,
                        projectIdHint,
                        serviceName,
                        resourceAttributes,
                        resourceMetrics,
                        scopeMetrics,
                        metadata,
                        scopeAttributes) with
                    {
                        Count = point.Count,
                        Sum = point.Sum,
                        SummaryQuantiles =
                        [
                            .. point.QuantileValues.Select(static value =>
                                new MetricSummaryQuantileIngestionRecord(value.Quantile, value.Value))
                        ]
                    });
                }

                break;
        }
    }

    private static MetricIngestionRecord CreateNumberMetricRecord(
        ProtoMetric metric,
        int metricType,
        ProtoNumberDataPoint point,
        string? projectIdHint,
        string serviceName,
        Dictionary<string, OtlpAttributeValue> resourceAttributes,
        ProtoResourceMetrics resourceMetrics,
        ProtoScopeMetrics scopeMetrics,
        IReadOnlyDictionary<string, OtlpAttributeValue> metadata,
        IReadOnlyDictionary<string, OtlpAttributeValue> scopeAttributes,
        bool? isMonotonic,
        int? aggregationTemporality)
    {
        var record = CreateMetricRecord(
            metric,
            metricType,
            point.TimeUnixNano,
            point.StartTimeUnixNano,
            point.Flags,
            point.Attributes,
            point.Exemplars,
            projectIdHint,
            serviceName,
            resourceAttributes,
            resourceMetrics,
            scopeMetrics,
            metadata,
            scopeAttributes) with
        {
            IsMonotonic = isMonotonic,
            AggregationTemporality = aggregationTemporality
        };

        return point.ValueCase switch
        {
            ProtoNumberDataPoint.ValueOneofCase.AsDouble => record with { DoubleValue = point.AsDouble },
            ProtoNumberDataPoint.ValueOneofCase.AsInt => record with { IntValue = point.AsInt },
            _ => throw new InvalidDataException($"OTLP metric '{metric.Name}' has a number point without a value.")
        };
    }

    private static MetricIngestionRecord CreateMetricRecord(
        ProtoMetric metric,
        int metricType,
        ulong timeUnixNano,
        ulong startTimeUnixNano,
        uint flags,
        RepeatedField<ProtoKeyValue> attributes,
        RepeatedField<ProtoExemplar>? exemplars,
        string? projectIdHint,
        string serviceName,
        Dictionary<string, OtlpAttributeValue> resourceAttributes,
        ProtoResourceMetrics resourceMetrics,
        ProtoScopeMetrics scopeMetrics,
        IReadOnlyDictionary<string, OtlpAttributeValue> metadata,
        IReadOnlyDictionary<string, OtlpAttributeValue> scopeAttributes)
    {
        if (timeUnixNano is 0)
            throw new InvalidDataException($"OTLP metric '{metric.Name}' has a data point with time_unix_nano=0.");

        var scope = scopeMetrics.Scope;
        return new MetricIngestionRecord
        {
            ProjectIdHint = projectIdHint,
            MetricName = metric.Name,
            MetricType = metricType,
            Unit = NullIfEmpty(metric.Unit),
            Description = NullIfEmpty(metric.Description),
            Metadata = metadata,
            ResourceSchemaUrl = NullIfEmpty(resourceMetrics.SchemaUrl),
            ResourceDroppedAttributesCount = resourceMetrics.Resource?.DroppedAttributesCount ?? 0,
            HasInstrumentationScope = scope is not null,
            ScopeName = NullIfEmpty(scope?.Name),
            ScopeVersion = NullIfEmpty(scope?.Version),
            ScopeAttributes = scopeAttributes,
            ScopeDroppedAttributesCount = scope?.DroppedAttributesCount ?? 0,
            ScopeSchemaUrl = NullIfEmpty(scopeMetrics.SchemaUrl),
            TimeUnixNano = timeUnixNano,
            StartTimeUnixNano = startTimeUnixNano,
            Flags = flags,
            Exemplars = ConvertMetricExemplars(exemplars),
            ServiceName = serviceName,
            Attributes = ExtractMetricAttributes(attributes),
            ResourceAttributes = resourceAttributes
        };
    }

    private static IReadOnlyList<MetricExemplarIngestionRecord> ConvertMetricExemplars(
        RepeatedField<ProtoExemplar>? exemplars)
    {
        if (exemplars is not { Count: > 0 }) return [];

        var result = new List<MetricExemplarIngestionRecord>(exemplars.Count);
        foreach (var exemplar in exemplars)
        {
            if (exemplar.TimeUnixNano is 0)
                throw new InvalidDataException("OTLP metric exemplar has time_unix_nano=0.");

            var converted = new MetricExemplarIngestionRecord
            {
                TimeUnixNano = exemplar.TimeUnixNano,
                SpanId = RequireIdOrAbsent(exemplar.SpanId, 8, "exemplar.span_id"),
                TraceId = RequireIdOrAbsent(exemplar.TraceId, 16, "exemplar.trace_id"),
                FilteredAttributes = ExtractMetricAttributes(exemplar.FilteredAttributes)
            };
            converted = exemplar.ValueCase switch
            {
                ProtoExemplar.ValueOneofCase.AsDouble => converted with { DoubleValue = exemplar.AsDouble },
                ProtoExemplar.ValueOneofCase.AsInt => converted with { IntValue = exemplar.AsInt },
                _ => throw new InvalidDataException("OTLP metric exemplar does not contain a numeric value.")
            };
            result.Add(converted);
        }

        return result;
    }

    private static MetricExponentialHistogramBucketsIngestionRecord ConvertExponentialBuckets(
        global::OpenTelemetry.Proto.Metrics.V1.ExponentialHistogramDataPoint.Types.Buckets? buckets) =>
        buckets is null
            ? new MetricExponentialHistogramBucketsIngestionRecord(0, [])
            : new MetricExponentialHistogramBucketsIngestionRecord(buckets.Offset, [.. buckets.BucketCounts]);

    private static int RequireAggregationTemporality(int temporality, string metricName) =>
        temporality is 1 or 2
            ? temporality
            : throw new InvalidDataException(
                $"OTLP metric '{metricName}' has an unspecified aggregation temporality.");

    private static void ValidateExplicitHistogramBuckets(int boundCount, int bucketCount, string metricName)
    {
        if ((bucketCount is 0 && boundCount is not 0) ||
            (bucketCount > 0 && bucketCount != boundCount + 1))
        {
            throw new InvalidDataException(
                $"OTLP metric '{metricName}' has an invalid explicit histogram bucket layout.");
        }
    }

    private static void ValidateSummaryQuantiles(
        RepeatedField<global::OpenTelemetry.Proto.Metrics.V1.SummaryDataPoint.Types.ValueAtQuantile> values,
        string metricName)
    {
        var previous = -1d;
        foreach (var value in values)
        {
            if (double.IsNaN(value.Quantile) || value.Quantile is < 0 or > 1 ||
                value.Quantile <= previous || double.IsNaN(value.Value) || value.Value < 0)
            {
                throw new InvalidDataException(
                    $"OTLP metric '{metricName}' has invalid summary quantile values.");
            }

            previous = value.Quantile;
        }
    }

    // Metric dimensions are instrumentation-defined attribute keys drawn from the same semantic
    // registries as span attributes, so the span allow-list is the persistence policy here too.
    private static Dictionary<string, OtlpAttributeValue> ExtractMetricAttributes(
        RepeatedField<ProtoKeyValue> attributes)
    {
        var dict = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        foreach (var attr in attributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;
            var renamed = DeprecatedAttributeNormalizer.TryNormalize(attr.Key, out var key);
            var value = ConvertProtoAnyValue(attr.Value);
            if (value is null) continue;
            if (renamed) dict.TryAdd(key, value);
            else dict[key] = value;
        }

        return dict;
    }

    #endregion

    #region Profiles Conversion

    public static ProfileIngestionBatch ConvertProfiles(ExportProfilesServiceRequest otlp)
    {
        var results = new List<ProfileIngestionRecord>();
        var dictionary = otlp.Dictionary ?? new ProtoProfilesDictionary();

        foreach (var resourceProfiles in otlp.ResourceProfiles)
        {
            var serviceName = ExtractServiceNameFromProto(resourceProfiles.Resource);
            var projectIdHint = ExtractProjectIdHintFromProto(resourceProfiles.Resource);

            var resourceAttrs = ExtractResourceAttributesFromProto(resourceProfiles.Resource);
            var schemaUrl = resourceProfiles.SchemaUrl;

            foreach (var scopeProfiles in resourceProfiles.ScopeProfiles)
            {
                var effectiveSchemaUrl = !string.IsNullOrEmpty(scopeProfiles.SchemaUrl)
                    ? scopeProfiles.SchemaUrl
                    : schemaUrl;

                foreach (var profile in scopeProfiles.Profiles)
                {
                    results.Add(CreateProfileRecord(profile, dictionary, projectIdHint, serviceName, resourceAttrs,
                        effectiveSchemaUrl));
                }
            }
        }

        return new ProfileIngestionBatch(results);
    }

    private static ProfileIngestionRecord CreateProfileRecord(
        ProtoProfile profile,
        ProtoProfilesDictionary dictionary,
        string? projectIdHint,
        string serviceName,
        Dictionary<string, OtlpAttributeValue> resourceAttributes,
        string? schemaUrl)
    {
        var profileId = RequireIdOrAbsent(profile.ProfileId, 16, "profile_id") ?? "";
        var sessionId = ExtractProfileSessionId(profile.AttributeIndices, dictionary);
        var attributes = ExtractProfileAttributes(profile.AttributeIndices, dictionary);

        var (traceId, spanId) = ResolveProfileLink(profile, dictionary);

        var sampleType = Resolve(profile.SampleType?.TypeStrindex ?? 0, dictionary);
        var sampleUnit = Resolve(profile.SampleType?.UnitStrindex ?? 0, dictionary);

        var functions = MapTable(dictionary.FunctionTable, (f, i) =>
            new ProfileFunctionIngestionRecord
            {
                Ordinal = i,
                Name = Resolve(f.NameStrindex, dictionary),
                SystemName = Resolve(f.SystemNameStrindex, dictionary),
                Filename = Resolve(f.FilenameStrindex, dictionary),
                StartLine = f.StartLine
            });

        var locations = MapTable(dictionary.LocationTable, static (loc, i) =>
        {
            List<ProfileLocationLineJson>? lines = null;
            if (loc.Line.Count > 0)
            {
                lines = new List<ProfileLocationLineJson>(loc.Line.Count);
                foreach (var line in loc.Line)
                {
                    lines.Add(new ProfileLocationLineJson(line.FunctionIndex, line.Line_, line.Column));
                }
            }

            return new ProfileLocationIngestionRecord
            {
                Ordinal = i,
                MappingOrdinal = loc.MappingIndex,
                Address = loc.Address,
                Lines = lines
            };
        });

        var mappings = MapTable(dictionary.MappingTable, (m, i) =>
            new ProfileMappingIngestionRecord
            {
                Ordinal = i,
                Filename = Resolve(m.FilenameStrindex, dictionary),
                MemoryStart = m.MemoryStart,
                MemoryLimit = m.MemoryLimit,
                FileOffset = m.FileOffset
            });

        var samples = MapTable(profile.Sample, (s, i) =>
        {
            string? linkTraceId = null;
            string? linkSpanId = null;
            if (s.LinkIndex > 0 && s.LinkIndex < dictionary.LinkTable.Count)
            {
                var link = dictionary.LinkTable[s.LinkIndex];
                linkTraceId = RequireIdOrAbsent(link.TraceId, 16, "link.trace_id");
                linkSpanId = RequireIdOrAbsent(link.SpanId, 8, "link.span_id");
            }

            return new ProfileSampleIngestionRecord
            {
                Ordinal = i,
                StackOrdinal = s.StackIndex,
                LinkTraceId = linkTraceId,
                LinkSpanId = linkSpanId,
                Values = s.Values.Count > 0 ? s.Values.ToArray() : null,
                TimestampsUnixNano = s.TimestampsUnixNano.Count > 0 ? s.TimestampsUnixNano.ToArray() : null
            };
        });

        var stacks = MapTable(dictionary.StackTable, static (st, i) =>
            new ProfileStackIngestionRecord
            {
                Ordinal = i,
                LocationOrdinals = st.LocationIndices.Count > 0 ? st.LocationIndices.ToArray() : null
            });

        return new ProfileIngestionRecord
        {
            ProjectIdHint = projectIdHint,
            ProfileId = profileId,
            TraceId = traceId,
            SpanId = spanId,
            SessionId = sessionId,
            TimeUnixNano = profile.TimeUnixNano,
            DurationNano = profile.DurationNano,
            SampleCount = profile.Sample.Count,
            SampleType = sampleType,
            SampleUnit = sampleUnit,
            OriginalPayloadFormat = NullIfEmpty(profile.OriginalPayloadFormat),
            ServiceName = serviceName,
            Attributes = attributes,
            ResourceAttributes = resourceAttributes,
            SchemaUrl = schemaUrl,
            Functions = functions,
            Locations = locations,
            Mappings = mappings,
            Samples = samples,
            Stacks = stacks
        };
    }

    // Projects a protobuf repeated table into pre-sized ingestion records, removing the repeated
    // new-List/for/Add scaffolding. Projections that resolve *_strindex fields capture the string
    // table `dictionary` (profile ingest is low-frequency, not the per-attribute hot path); the
    // ones that don't stay static. The literal `Resolve(x.YStrindex, dictionary)` shape is also
    // pinned by BuildVerify.VerifyOtlpProfileSymbolsAreResolved.
    private static List<TResult> MapTable<TSource, TResult>(
        IReadOnlyList<TSource> source,
        Func<TSource, int, TResult> project)
    {
        var result = new List<TResult>(source.Count);
        for (var i = 0; i < source.Count; i++)
            result.Add(project(source[i], i));
        return result;
    }

    private static Dictionary<string, OtlpAttributeValue> ExtractProfileAttributes(
        RepeatedField<int> indices,
        ProtoProfilesDictionary dictionary)
    {
        var attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);

        foreach (var index in indices)
        {
            if (index < 0 || index >= dictionary.AttributeTable.Count)
                continue;

            var attribute = dictionary.AttributeTable[index];
            var key = Resolve(attribute.KeyStrindex, dictionary);
            if (string.IsNullOrEmpty(key) ||
                !AttributeKeySets.IsSafeProfileAttribute(key))
            {
                continue;
            }

            var value = ConvertProtoAnyValue(attribute.Value);
            if (value is not null)
                attributes[key] = value;
        }

        return attributes;
    }

    private static string? ExtractProfileSessionId(
        RepeatedField<int> indices,
        ProtoProfilesDictionary dictionary)
    {
        foreach (var index in indices)
        {
            if (index < 0 || index >= dictionary.AttributeTable.Count)
                continue;

            var attribute = dictionary.AttributeTable[index];
            var key = Resolve(attribute.KeyStrindex, dictionary);
            if (string.IsNullOrEmpty(key) ||
                !key.IsAny(AttributeKeySets.SessionCorrelation) ||
                attribute.Value.ValueCase is not ProtoAnyValue.ValueOneofCase.StringValue)
            {
                continue;
            }

            return attribute.Value.StringValue;
        }

        return null;
    }

    private static (string? TraceId, string? SpanId) ResolveProfileLink(
        ProtoProfile profile,
        ProtoProfilesDictionary dictionary)
    {
        foreach (var sample in profile.Sample)
        {
            if (sample.LinkIndex <= 0 || sample.LinkIndex >= dictionary.LinkTable.Count)
                continue;

            var link = dictionary.LinkTable[sample.LinkIndex];
            if (link.TraceId.Length > 0 || link.SpanId.Length > 0)
            {
                return (RequireIdOrAbsent(link.TraceId, 16, "link.trace_id"),
                    RequireIdOrAbsent(link.SpanId, 8, "link.span_id"));
            }
        }

        foreach (var link in dictionary.LinkTable)
        {
            if (link.TraceId.Length > 0 || link.SpanId.Length > 0)
            {
                return (RequireIdOrAbsent(link.TraceId, 16, "link.trace_id"),
                    RequireIdOrAbsent(link.SpanId, 8, "link.span_id"));
            }
        }

        return (null, null);
    }

    private static string? Resolve(int index, ProtoProfilesDictionary dictionary) =>
        index >= 0 && index < dictionary.StringTable.Count
            ? NullIfEmpty(dictionary.StringTable[index])
            : null;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    #endregion
}
