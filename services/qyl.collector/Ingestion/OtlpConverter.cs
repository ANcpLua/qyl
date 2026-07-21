using System.Text;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using ProtoAnyValue = OpenTelemetry.Proto.Common.V1.AnyValue;
using ProtoArrayValue = OpenTelemetry.Proto.Common.V1.ArrayValue;
using ProtoKeyValue = OpenTelemetry.Proto.Common.V1.KeyValue;
using ProtoKeyValueList = OpenTelemetry.Proto.Common.V1.KeyValueList;
using ProtoLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;
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
            var resource = ExtractResourceProjection(resourceSpan.Resource);
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
                            resource.Attributes,
                            resource.EntityRefs,
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

    private static ResourceProjection ExtractResourceProjection(ProtoResource? resource)
    {
        var attrs = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        if (resource is null) return new ResourceProjection(attrs, []);

        var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
        var projectedKeys = new Dictionary<string, ResourceAttributeProjectionKey>(StringComparer.Ordinal);
        var entityReferencedKeys = GetResourceEntityReferencedKeys(resource);

        foreach (var attr in resource.Attributes)
        {
            if (string.IsNullOrWhiteSpace(attr.Key))
                throw new InvalidDataException("OTLP resource attribute key must not be empty.");
            if (!sourceKeys.Add(attr.Key))
                throw new InvalidDataException($"OTLP resource contains duplicate attribute key '{attr.Key}'.");

            var renamed = DeprecatedAttributeNormalizer.TryNormalize(attr.Key, out var key);
            if (!AttributeKeySets.IsSafeResourceAttribute(key) &&
                (!entityReferencedKeys.Contains(attr.Key) ||
                 !AttributeKeySets.IsSafeEntityReferencedResourceAttribute(key)))
            {
                continue;
            }

            var value = ConvertProtoAnyValue(attr.Value);
            SetNormalizedAttribute(attrs, key, value, renamed);

            projectedKeys.Add(attr.Key, new ResourceAttributeProjectionKey(key));
        }

        return new ResourceProjection(attrs, ExtractResourceEntityRefs(resource, projectedKeys, attrs));
    }

    private static HashSet<string> GetResourceEntityReferencedKeys(ProtoResource resource)
    {
        var referencedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entityRef in resource.EntityRefs)
        {
            referencedKeys.UnionWith(entityRef.IdKeys);
            referencedKeys.UnionWith(entityRef.DescriptionKeys);
        }

        return referencedKeys;
    }

    private static IReadOnlyList<ResourceEntityRefIngestionRecord> ExtractResourceEntityRefs(
        ProtoResource resource,
        IReadOnlyDictionary<string, ResourceAttributeProjectionKey> projectedKeys,
        IReadOnlyDictionary<string, OtlpAttributeValue> projectedAttributes)
    {
        if (resource.EntityRefs.Count is 0) return [];

        var entityRefs = new List<(string Identity, ResourceEntityRefIngestionRecord Value)>(
            resource.EntityRefs.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entityRef in resource.EntityRefs)
        {
            if (string.IsNullOrWhiteSpace(entityRef.Type))
                throw new InvalidDataException("OTLP resource entity type must not be empty.");
            if (entityRef.IdKeys.Count is 0)
                throw new InvalidDataException("OTLP resource entity must contain at least one id_key.");

            var idKeys = NormalizeEntityReferenceKeys(entityRef.IdKeys, projectedKeys, "id_key");
            var descriptionKeys = NormalizeEntityReferenceKeys(
                entityRef.DescriptionKeys,
                projectedKeys,
                "description_key");
            var value = new ResourceEntityRefIngestionRecord(
                NullIfEmpty(entityRef.SchemaUrl),
                entityRef.Type,
                idKeys,
                descriptionKeys);
            var identity = GetEntityReferenceIdentity(value, projectedAttributes);
            if (!identities.Add(identity))
                throw new InvalidDataException("OTLP resource contains a duplicate entity reference.");
            entityRefs.Add((identity, value));
        }

        entityRefs.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Identity, right.Identity));
        return [.. entityRefs.Select(static item => item.Value)];
    }

    private static IReadOnlyList<string> NormalizeEntityReferenceKeys(
        RepeatedField<string> keys,
        IReadOnlyDictionary<string, ResourceAttributeProjectionKey> projectedKeys,
        string fieldName)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidDataException($"OTLP resource entity {fieldName} must not be empty.");
            if (!projectedKeys.TryGetValue(key, out var projection))
            {
                throw new InvalidDataException(
                    $"OTLP resource entity {fieldName} '{key}' does not reference a persisted resource attribute.");
            }

            var projectedKey = projection.PersistedKey;
            if (!normalized.Add(projectedKey))
            {
                throw new InvalidDataException(
                    $"OTLP resource entity contains duplicate {fieldName} '{projectedKey}'.");
            }
        }

        return [.. normalized.Order(StringComparer.Ordinal)];
    }

    private static string GetEntityReferenceIdentity(
        ResourceEntityRefIngestionRecord entityRef,
        IReadOnlyDictionary<string, OtlpAttributeValue> resourceAttributes)
    {
        var builder = new StringBuilder();
        AppendCanonicalSegment(builder, entityRef.Type);
        builder.Append(entityRef.IdKeys.Count.ToString(CultureInfo.InvariantCulture)).Append(':');
        foreach (var key in entityRef.IdKeys)
        {
            AppendCanonicalSegment(builder, key);
            AppendCanonicalSegment(builder, resourceAttributes[key].ToIdentityString());
        }

        return builder.ToString();
    }

    private sealed record ResourceProjection(
        Dictionary<string, OtlpAttributeValue> Attributes,
        IReadOnlyList<ResourceEntityRefIngestionRecord> EntityRefs);

    private readonly record struct ResourceAttributeProjectionKey(string PersistedKey);

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
            SetNormalizedAttribute(attributes, key, value, renamed);
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
            SetNormalizedAttribute(attributes, key, value, renamed);
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

    private static OtlpAttributeValue ConvertProtoAnyValue(ProtoAnyValue? value)
    {
        if (value is null) return OtlpAttributeValue.Empty;

        return value.ValueCase switch
        {
            ProtoAnyValue.ValueOneofCase.StringValue => OtlpAttributeValue.FromString(value.StringValue),
            ProtoAnyValue.ValueOneofCase.IntValue => OtlpAttributeValue.FromInt(value.IntValue),
            ProtoAnyValue.ValueOneofCase.DoubleValue => OtlpAttributeValue.FromDouble(value.DoubleValue),
            ProtoAnyValue.ValueOneofCase.BoolValue => OtlpAttributeValue.FromBool(value.BoolValue),
            ProtoAnyValue.ValueOneofCase.BytesValue => OtlpAttributeValue.FromBytes(CopyBytes(value.BytesValue)),
            ProtoAnyValue.ValueOneofCase.ArrayValue => ConvertProtoArrayValue(value.ArrayValue),
            ProtoAnyValue.ValueOneofCase.KvlistValue => ConvertProtoKeyValueList(value.KvlistValue),
            ProtoAnyValue.ValueOneofCase.None => OtlpAttributeValue.Empty,
            _ => throw new InvalidDataException("OTLP AnyValue contains an unknown value kind.")
        };
    }

    private static OtlpAttributeValue ConvertProtoArrayValue(ProtoArrayValue value)
    {
        var items = new List<OtlpAttributeValue>(value.Values.Count);
        foreach (var item in value.Values)
        {
            items.Add(ConvertProtoAnyValue(item));
        }

        return OtlpAttributeValue.FromArray(items);
    }

    private static OtlpAttributeValue ConvertProtoKeyValueList(ProtoKeyValueList value)
    {
        var items = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        foreach (var item in value.Values)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
                throw new InvalidDataException("OTLP key-value-list contains an empty key.");

            if (!items.TryAdd(item.Key, ConvertProtoAnyValue(item.Value)))
                throw new InvalidDataException($"OTLP key-value-list contains duplicate key '{item.Key}'.");
        }

        return OtlpAttributeValue.FromKeyValueList(items);
    }

    private static SpanIngestionRecord CreateSpanRecordFromProto(
        ProtoSpan span,
        string? projectIdHint,
        string serviceName,
        Dictionary<string, OtlpAttributeValue> attributes,
        Dictionary<string, OtlpAttributeValue> resourceAttributes,
        IReadOnlyList<ResourceEntityRefIngestionRecord> resourceEntityRefs,
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
            ResourceEntityRefs = resourceEntityRefs,
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
            var resource = ExtractResourceProjection(resourceLogs.Resource);

            foreach (var scopeLogs in resourceLogs.ScopeLogs)
            foreach (var log in scopeLogs.LogRecords)
            {
                logs.Add(CreateLogRecord(
                    log,
                    projectIdHint,
                    serviceName,
                    resource.Attributes,
                    resource.EntityRefs));
            }
        }

        return new LogIngestionBatch(logs);
    }

    private static LogIngestionRecord CreateLogRecord(
        ProtoLogRecord log,
        string? projectIdHint,
        string serviceName,
        Dictionary<string, OtlpAttributeValue> resourceAttributes,
        IReadOnlyList<ResourceEntityRefIngestionRecord> resourceEntityRefs)
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
            BodyText = ConvertProtoAnyValue(log.Body).ToStableString(),
            ServiceName = serviceName,
            Attributes = attributes,
            ResourceAttributes = resourceAttributes,
            ResourceEntityRefs = resourceEntityRefs
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
            SetNormalizedAttribute(dict, key, value, renamed);
        }

        return dict;
    }

    #endregion

    private static void AppendCanonicalSegment(StringBuilder builder, string value) =>
        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);

    private static void SetNormalizedAttribute(
        Dictionary<string, OtlpAttributeValue> attributes,
        string key,
        OtlpAttributeValue value,
        bool renamed)
    {
        if (renamed) attributes.TryAdd(key, value);
        else attributes[key] = value;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
