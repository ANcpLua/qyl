
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Collector.Trace.V1;
using ProtoAnyValue = OpenTelemetry.Proto.Common.V1.AnyValue;
using ProtoArrayValue = OpenTelemetry.Proto.Common.V1.ArrayValue;
using ProtoKeyValue = OpenTelemetry.Proto.Common.V1.KeyValue;
using ProtoKeyValueList = OpenTelemetry.Proto.Common.V1.KeyValueList;
using ProtoLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;
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
            if (string.IsNullOrEmpty(attr.Key) ||
                !AttributeKeySets.IsSafeResourceAttribute(attr.Key))
            {
                continue;
            }

            var value = ConvertProtoAnyValue(attr.Value);
            if (value is not null) attrs[attr.Key] = value;
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
            if (string.IsNullOrEmpty(attr.Key) ||
                !AttributeKeySets.ShouldCaptureSpanAttribute(attr.Key))
            {
                continue;
            }

            var value = ConvertProtoAnyValue(attr.Value);
            if (value is not null) attributes[attr.Key] = value;
        }

        return attributes;
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
            SpanId = ToHex(span.SpanId) ?? "",
            TraceId = ToHex(span.TraceId) ?? "",
            ParentSpanId = ToHex(span.ParentSpanId),
            Name = span.Name ?? "unknown",
            Kind = (int)span.Kind,
            StartTimeUnixNano = span.StartTimeUnixNano,
            EndTimeUnixNano = span.EndTimeUnixNano,
            StatusCode = span.Status is not null ? (int)span.Status.Code : null,
            ServiceName = serviceName,
            Attributes = attributes,
            ResourceAttributes = resourceAttributes,
            SchemaUrl = schemaUrl
        };

    #endregion

    #region Shared Helpers

    private static string? ToHex(ByteString value)
    {
        if (value.Length is 0) return null;
        return Convert.ToHexString(value.Span).ToLowerInvariant();
    }

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
            TraceId = ToHex(log.TraceId),
            SpanId = ToHex(log.SpanId),
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
            if (string.IsNullOrEmpty(attr.Key) ||
                (!AttributeKeySets.IsSafeLogAttribute(attr.Key) &&
                 !attr.Key.IsAny(AttributeKeySets.SessionCorrelation)))
            {
                continue;
            }

            var value = ConvertProtoAnyValue(attr.Value);
            if (value is not null)
                dict[attr.Key] = value;
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
        var profileId = ToHex(profile.ProfileId) ?? Guid.NewGuid().ToString("N")[..16];
        var sessionId = ExtractProfileSessionId(profile.AttributeIndices, dictionary);
        var attributes = ExtractProfileAttributes(profile.AttributeIndices, dictionary);

        var (traceId, spanId) = ResolveProfileLink(profile, dictionary);

        var sampleType = Resolve(profile.SampleType?.TypeStrindex ?? 0, dictionary);
        var sampleUnit = Resolve(profile.SampleType?.UnitStrindex ?? 0, dictionary);

        var functions = new List<ProfileFunctionIngestionRecord>(dictionary.FunctionTable.Count);
        for (var i = 0; i < dictionary.FunctionTable.Count; i++)
        {
            var f = dictionary.FunctionTable[i];
            functions.Add(new ProfileFunctionIngestionRecord
            {
                Ordinal = i,
                Name = Resolve(f.NameStrindex, dictionary),
                SystemName = Resolve(f.SystemNameStrindex, dictionary),
                Filename = Resolve(f.FilenameStrindex, dictionary),
                StartLine = f.StartLine
            });
        }

        var locations = new List<ProfileLocationIngestionRecord>(dictionary.LocationTable.Count);
        for (var i = 0; i < dictionary.LocationTable.Count; i++)
        {
            var loc = dictionary.LocationTable[i];
            List<ProfileLocationLineJson>? lines = null;
            if (loc.Line.Count > 0)
            {
                lines = new List<ProfileLocationLineJson>(loc.Line.Count);
                foreach (var line in loc.Line)
                {
                    lines.Add(new ProfileLocationLineJson(line.FunctionIndex, line.Line_, line.Column));
                }
            }

            locations.Add(new ProfileLocationIngestionRecord
            {
                Ordinal = i,
                MappingOrdinal = loc.MappingIndex,
                Address = loc.Address,
                Lines = lines
            });
        }

        var mappings = new List<ProfileMappingIngestionRecord>(dictionary.MappingTable.Count);
        for (var i = 0; i < dictionary.MappingTable.Count; i++)
        {
            var m = dictionary.MappingTable[i];
            mappings.Add(new ProfileMappingIngestionRecord
            {
                Ordinal = i,
                Filename = Resolve(m.FilenameStrindex, dictionary),
                MemoryStart = m.MemoryStart,
                MemoryLimit = m.MemoryLimit,
                FileOffset = m.FileOffset
            });
        }

        var samples = new List<ProfileSampleIngestionRecord>(profile.Sample.Count);
        for (var i = 0; i < profile.Sample.Count; i++)
        {
            var s = profile.Sample[i];
            string? linkTraceId = null;
            string? linkSpanId = null;
            if (s.LinkIndex > 0 && s.LinkIndex < dictionary.LinkTable.Count)
            {
                var link = dictionary.LinkTable[s.LinkIndex];
                linkTraceId = ToHex(link.TraceId);
                linkSpanId = ToHex(link.SpanId);
            }

            samples.Add(new ProfileSampleIngestionRecord
            {
                Ordinal = i,
                StackOrdinal = s.StackIndex,
                LinkTraceId = linkTraceId,
                LinkSpanId = linkSpanId,
                Values = s.Values.Count > 0 ? s.Values.ToArray() : null,
                TimestampsUnixNano = s.TimestampsUnixNano.Count > 0 ? s.TimestampsUnixNano.ToArray() : null
            });
        }

        var stacks = new List<ProfileStackIngestionRecord>(dictionary.StackTable.Count);
        for (var i = 0; i < dictionary.StackTable.Count; i++)
        {
            var st = dictionary.StackTable[i];
            stacks.Add(new ProfileStackIngestionRecord
            {
                Ordinal = i,
                LocationOrdinals = st.LocationIndices.Count > 0 ? st.LocationIndices.ToArray() : null
            });
        }

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
                return (ToHex(link.TraceId), ToHex(link.SpanId));
        }

        foreach (var link in dictionary.LinkTable)
        {
            if (link.TraceId.Length > 0 || link.SpanId.Length > 0)
                return (ToHex(link.TraceId), ToHex(link.SpanId));
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
