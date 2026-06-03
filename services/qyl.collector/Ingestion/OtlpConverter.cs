
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Collector.Trace.V1;
using QylGenAiCostProcessor = Qyl.Instrumentation.Instrumentation.GenAi.QylGenAiCostProcessor;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;
using ProfileAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Profile.ProfileAttributes;
using ProtoAnyValue = OpenTelemetry.Proto.Common.V1.AnyValue;
using ProtoKeyValue = OpenTelemetry.Proto.Common.V1.KeyValue;
using ProtoLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;
using ProtoProfile = OpenTelemetry.Proto.Profiles.V1Development.Profile;
using ProtoProfilesDictionary = OpenTelemetry.Proto.Profiles.V1Development.ProfilesDictionary;
using ProtoResource = OpenTelemetry.Proto.Resource.V1.Resource;
using ProtoSpan = OpenTelemetry.Proto.Trace.V1.Span;
using ServiceAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Service.ServiceAttributes;

namespace Qyl.Collector.Ingestion;

internal static class OtlpConverter
{
    #region OTLP Trace Conversion

    public static List<SpanStorageRow> ConvertTraceRequestToStorageRows(ExportTraceServiceRequest request)
    {
        var spans = new List<SpanStorageRow>();

        foreach (var resourceSpan in request.ResourceSpans)
        {
            var serviceName = ExtractServiceNameFromProto(resourceSpan.Resource);
            var resourceAttrs = ExtractResourceAttributesFromProto(resourceSpan.Resource);
            var resourceJson = PersistedAttributePolicy.SerializeResourceAttributes(resourceAttrs);
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
                        CreateStorageRowFromProto(span, serviceName, attributes, effectiveSchemaUrl, resourceJson);
                    spans.Add(spanRecord);
                }
            }
        }

        return spans;
    }

    private static string ExtractServiceNameFromProto(ProtoResource? resource)
    {
        if (resource is null) return "unknown";

        foreach (var attr in resource.Attributes)
        {
            if (attr is
                {
                    Key: ServiceAttributes.Name,
                    Value.ValueCase: ProtoAnyValue.ValueOneofCase.StringValue
                })
                return attr.Value.StringValue;
        }

        return "unknown";
    }

    private static Dictionary<string, string> ExtractResourceAttributesFromProto(ProtoResource? resource)
    {
        var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (resource is null) return attrs;

        foreach (var attr in resource.Attributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;
            var value = ConvertProtoAnyValueToString(attr.Value);
            if (value is not null) attrs[attr.Key] = value;
        }

        return attrs;
    }

    private static Dictionary<string, string> ExtractAttributesFromProto(
        RepeatedField<ProtoKeyValue> protoAttributes,
        string serviceName)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal) { [ServiceAttributes.Name] = serviceName };

        foreach (var attr in protoAttributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;

            var value = ConvertProtoAnyValueToString(attr.Value);
            if (value is not null) attributes[attr.Key] = value;
        }

        return attributes;
    }

    private static string? ConvertProtoAnyValueToString(ProtoAnyValue? value)
    {
        if (value is null) return null;

        return value.ValueCase switch
        {
            ProtoAnyValue.ValueOneofCase.StringValue => value.StringValue,
            ProtoAnyValue.ValueOneofCase.IntValue => value.IntValue.ToString(CultureInfo.InvariantCulture),
            ProtoAnyValue.ValueOneofCase.DoubleValue => value.DoubleValue.ToString(CultureInfo.InvariantCulture),
            ProtoAnyValue.ValueOneofCase.BoolValue => value.BoolValue.ToString().ToLowerInvariant(),
            ProtoAnyValue.ValueOneofCase.BytesValue => Convert.ToBase64String(value.BytesValue.Span),
            ProtoAnyValue.ValueOneofCase.ArrayValue => SerializeProtoArray(value.ArrayValue.Values),
            ProtoAnyValue.ValueOneofCase.KvlistValue => SerializeProtoKeyValueList(value.KvlistValue.Values),
            _ => null
        };
    }

    private static SpanStorageRow CreateStorageRowFromProto(
        ProtoSpan span,
        string serviceName,
        Dictionary<string, string> attributes,
        string? schemaUrl,
        string? resourceJson) =>
        CreateStorageRow(
            ToHex(span.SpanId), ToHex(span.TraceId), ToHex(span.ParentSpanId), span.Name,
            (int)span.Kind, span.StartTimeUnixNano, span.EndTimeUnixNano,
            span.Status is not null ? (int)span.Status.Code : null, span.Status?.Message,
            serviceName, attributes, schemaUrl, resourceJson);

    #endregion

    #region Shared Helpers

    private static SpanStorageRow CreateStorageRow(
        string? spanId, string? traceId, string? parentSpanId, string? name,
        int? kind, ulong startNano, ulong endNano,
        int? statusCode, string? statusMessage,
        string serviceName, Dictionary<string, string> attributes,
        string? schemaUrl, string? resourceJson)
    {
        var durationNs = endNano >= startNano ? endNano - startNano : 0UL;
        var genAi = ExtractGenAiAttributes(attributes);
        return new SpanStorageRow
        {
            SpanId = spanId ?? "",
            TraceId = traceId ?? "",
            ParentSpanId = string.IsNullOrEmpty(parentSpanId) ? null : parentSpanId,
            SessionId = attributes.GetFirstValueOrDefault(AttributeKeySets.SessionCorrelation),
            Name = name ?? "unknown",
            Kind = ConvertSpanKindToByte(kind),
            StartTimeUnixNano = startNano,
            EndTimeUnixNano = endNano,
            DurationNs = durationNs,
            StatusCode = ConvertStatusCodeToByte(statusCode),
            StatusMessage = string.IsNullOrEmpty(statusMessage) ? null : statusMessage,
            ServiceName = serviceName,
            GenAiProviderName = genAi.ProviderName,
            GenAiRequestModel = genAi.RequestModel,
            GenAiResponseModel = genAi.ResponseModel,
            GenAiInputTokens = genAi.TokensIn,
            GenAiOutputTokens = genAi.TokensOut,
            GenAiTemperature = genAi.Temperature,
            GenAiStopReason = genAi.StopReason,
            GenAiToolName = genAi.ToolName,
            GenAiToolCallId = genAi.ToolCallId,
            GenAiCostUsd = genAi.CostUsd,
            AttributesJson = PersistedAttributePolicy.SerializeSpanAttributes(attributes),
            ResourceJson = resourceJson,
            BaggageJson = null,
            SchemaUrl = schemaUrl
        };
    }

    private static string SerializeProtoArray(RepeatedField<ProtoAnyValue> values)
    {
        var items = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (ConvertProtoAnyValueToString(value) is { } converted)
                items.Add(converted);
        }

        return JsonSerializer.Serialize(items, QylSerializerContext.Default.StringList);
    }

    private static string SerializeProtoKeyValueList(RepeatedField<ProtoKeyValue> values)
    {
        var dict = new Dictionary<string, string>(values.Count, StringComparer.Ordinal);
        foreach (var kv in values)
        {
            if (string.IsNullOrEmpty(kv.Key))
                continue;

            if (ConvertProtoAnyValueToString(kv.Value) is { } converted)
                dict[kv.Key] = converted;
        }

        return JsonSerializer.Serialize(dict, QylSerializerContext.Default.DictionaryStringString);
    }

    private static string? ToHex(ByteString value)
    {
        if (value.Length is 0) return null;
        return Convert.ToHexString(value.Span).ToLowerInvariant();
    }

    private readonly record struct GenAiData(
        string? ProviderName,
        string? RequestModel,
        string? ResponseModel,
        long? TokensIn,
        long? TokensOut,
        double? Temperature,
        string? StopReason,
        string? ToolName,
        string? ToolCallId,
        double? CostUsd);

    private static GenAiData ExtractGenAiAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        var providerName = attributes.GetValueOrDefault(GenAiAttributes.ProviderName);

        var requestModel = attributes.GetValueOrDefault(GenAiAttributes.RequestModel);
        var responseModel = attributes.GetValueOrDefault(GenAiAttributes.ResponseModel);

        var tokensIn = ParseNullableLong(
            attributes.GetValueOrDefault(GenAiAttributes.UsageInputTokens));

        var tokensOut = ParseNullableLong(
            attributes.GetValueOrDefault(GenAiAttributes.UsageOutputTokens));

        var temperature = ParseNullableDouble(attributes.GetValueOrDefault(GenAiAttributes.RequestTemperature));
        var stopReason = attributes.GetValueOrDefault(GenAiAttributes.ResponseFinishReasons);
        var toolName = attributes.GetValueOrDefault(GenAiAttributes.ToolName);
        var toolCallId = attributes.GetValueOrDefault(GenAiAttributes.ToolCallId);
        var costUsd = ParseNullableDouble(attributes.GetValueOrDefault(QylGenAiCostProcessor.CostAttribute));

        return new GenAiData(
            providerName, requestModel, responseModel,
            tokensIn, tokensOut, temperature, stopReason,
            toolName, toolCallId, costUsd);
    }

    private static byte ConvertSpanKindToByte(int? kind) => kind switch
    {
        1 => 1,
        2 => 2,
        3 => 3,
        4 => 4,
        5 => 5,
        _ => 0
    };

    private static byte ConvertStatusCodeToByte(int? code) => code switch
    {
        1 => 1,
        2 => 2,
        _ => 0
    };

    private static long? ParseNullableLong(string? value) =>
        AttributeParsing.ParseNullableLong(value);

    private static double? ParseNullableDouble(string? value) =>
        string.IsNullOrEmpty(value)
            ? null
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result
                : null;

    #endregion

    #region Logs Conversion

    public static List<LogStorageRow> ConvertLogsToStorageRows(ExportLogsServiceRequest otlp)
    {
        var logs = new List<LogStorageRow>();

        foreach (var resourceLogs in otlp.ResourceLogs)
        {
            var serviceName = ExtractServiceNameFromProto(resourceLogs.Resource);

            var resourceAttrs = ExtractResourceAttributesFromProto(resourceLogs.Resource);
            var resourceJson = PersistedAttributePolicy.SerializeResourceAttributes(resourceAttrs);

            foreach (var scopeLogs in resourceLogs.ScopeLogs)
            foreach (var log in scopeLogs.LogRecords)
            {
                logs.Add(CreateLogStorageRow(log, serviceName, resourceJson));
            }
        }

        return logs;
    }

    private static LogStorageRow CreateLogStorageRow(
        ProtoLogRecord log,
        string serviceName,
        string? resourceJson)
    {
        var sessionId = ExtractSessionId(log.Attributes);

        var body = ConvertProtoAnyValueToSafeLogBody(log.Body);

        var severityNumber = (int)log.SeverityNumber;
        var severityText = string.IsNullOrEmpty(log.SeverityText)
            ? SeverityNumberToText(severityNumber)
            : log.SeverityText;

        return new LogStorageRow
        {
            LogId = Guid.NewGuid().ToString("N"),
            TraceId = ToHex(log.TraceId),
            SpanId = ToHex(log.SpanId),
            SessionId = sessionId,
            TimeUnixNano = log.TimeUnixNano,
            ObservedTimeUnixNano = log.ObservedTimeUnixNano > 0 ? log.ObservedTimeUnixNano : null,
            SeverityNumber = (byte)Math.Clamp(severityNumber, 0, 24),
            SeverityText = severityText,
            Body = body,
            ServiceName = serviceName,
            AttributesJson = SerializeProtoAttributes(log.Attributes),
            ResourceJson = resourceJson
        };
    }

    private static string? SerializeProtoAttributes(RepeatedField<ProtoKeyValue> attributes)
    {
        if (attributes.Count is 0) return null;

        var dict = new Dictionary<string, string>(attributes.Count, StringComparer.Ordinal);
        foreach (var attr in attributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;
            dict[attr.Key] = ConvertProtoAnyValueToString(attr.Value) ?? "";
        }

        return PersistedAttributePolicy.SerializeLogAttributes(dict);
    }

    private static string? ExtractSessionId(RepeatedField<ProtoKeyValue> attributes)
    {
        foreach (var attr in attributes)
        {
            if (attr.Key.IsAny(AttributeKeySets.SessionCorrelation) &&
                attr.Value.ValueCase is ProtoAnyValue.ValueOneofCase.StringValue)
            {
                return attr.Value.StringValue;
            }
        }

        return null;
    }

    private static string? ConvertProtoAnyValueToSafeLogBody(ProtoAnyValue? value)
    {
        var raw = ConvertProtoAnyValueToString(value);
        if (string.IsNullOrEmpty(raw))
            return raw;

        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        var fingerprint = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
        return $"sha256:{fingerprint};chars:{raw.Length};bytes:{bytes.Length}";
    }

    private static string SeverityNumberToText(int severityNumber) => severityNumber switch
    {
        >= 1 and <= 4 => "TRACE",
        >= 5 and <= 8 => "DEBUG",
        >= 9 and <= 12 => "INFO",
        >= 13 and <= 16 => "WARN",
        >= 17 and <= 20 => "ERROR",
        >= 21 => "FATAL",
        _ => "UNSPECIFIED"
    };

    #endregion

    #region Profiles Conversion

    public static List<ProfileConversionResult> ConvertProfilesToNormalizedRows(ExportProfilesServiceRequest otlp)
    {
        var results = new List<ProfileConversionResult>();
        var dictionary = otlp.Dictionary ?? new ProtoProfilesDictionary();

        foreach (var resourceProfiles in otlp.ResourceProfiles)
        {
            var serviceName = ExtractServiceNameFromProto(resourceProfiles.Resource);

            var resourceAttrs = ExtractResourceAttributesFromProto(resourceProfiles.Resource);
            var resourceJson = PersistedAttributePolicy.SerializeResourceAttributes(resourceAttrs);
            var schemaUrl = resourceProfiles.SchemaUrl;

            foreach (var scopeProfiles in resourceProfiles.ScopeProfiles)
            {
                var effectiveSchemaUrl = !string.IsNullOrEmpty(scopeProfiles.SchemaUrl)
                    ? scopeProfiles.SchemaUrl
                    : schemaUrl;

                foreach (var profile in scopeProfiles.Profiles)
                {
                    results.Add(CreateNormalizedProfile(profile, dictionary, serviceName, resourceJson,
                        effectiveSchemaUrl));
                }
            }
        }

        return results;
    }

    private static ProfileConversionResult CreateNormalizedProfile(
        ProtoProfile profile,
        ProtoProfilesDictionary dictionary,
        string serviceName,
        string? resourceJson,
        string? schemaUrl)
    {
        var profileId = ToHex(profile.ProfileId) ?? Guid.NewGuid().ToString("N")[..16];
        var attributes = ExtractProfileAttributes(profile.AttributeIndices, dictionary);

        var sessionId = attributes.GetFirstValueOrDefault(AttributeKeySets.SessionCorrelation);

        var profileFrameType = attributes.GetValueOrDefault(ProfileAttributes.FrameType);

        var (traceId, spanId) = ResolveProfileLink(profile, dictionary);

        var sampleType = Resolve(profile.SampleType?.TypeStrindex ?? 0, dictionary);
        var sampleUnit = Resolve(profile.SampleType?.UnitStrindex ?? 0, dictionary);

        var header = new ProfileStorageRow
        {
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
            ProfileFrameType = profileFrameType,
            AttributesJson = PersistedAttributePolicy.SerializeProfileAttributes(attributes),
            ResourceJson = resourceJson,
            ProfileDataJson = null,
            SchemaUrl = schemaUrl
        };

        var functions = new List<ProfileFunctionRow>(dictionary.FunctionTable.Count);
        for (var i = 0; i < dictionary.FunctionTable.Count; i++)
        {
            var f = dictionary.FunctionTable[i];
            functions.Add(new ProfileFunctionRow
            {
                ProfileId = profileId,
                Ordinal = i,
                Name = Resolve(f.NameStrindex, dictionary),
                SystemName = Resolve(f.SystemNameStrindex, dictionary),
                Filename = Resolve(f.FilenameStrindex, dictionary),
                StartLine = f.StartLine
            });
        }

        var locations = new List<ProfileLocationRow>(dictionary.LocationTable.Count);
        for (var i = 0; i < dictionary.LocationTable.Count; i++)
        {
            var loc = dictionary.LocationTable[i];
            string? linesJson = null;
            if (loc.Line.Count > 0)
            {
                var lines = new List<ProfileLocationLineJson>(loc.Line.Count);
                foreach (var line in loc.Line)
                {
                    lines.Add(new ProfileLocationLineJson(line.FunctionIndex, line.Line_, line.Column));
                }

                linesJson = JsonSerializer.Serialize(lines, QylSerializerContext.Default.ProfileLocationLineJsonList);
            }

            locations.Add(new ProfileLocationRow
            {
                ProfileId = profileId,
                Ordinal = i,
                MappingOrdinal = loc.MappingIndex,
                Address = loc.Address,
                LinesJson = linesJson
            });
        }

        var mappings = new List<ProfileMappingRow>(dictionary.MappingTable.Count);
        for (var i = 0; i < dictionary.MappingTable.Count; i++)
        {
            var m = dictionary.MappingTable[i];
            mappings.Add(new ProfileMappingRow
            {
                ProfileId = profileId,
                Ordinal = i,
                Filename = Resolve(m.FilenameStrindex, dictionary),
                MemoryStart = m.MemoryStart,
                MemoryLimit = m.MemoryLimit,
                FileOffset = m.FileOffset
            });
        }

        var samples = new List<ProfileSampleRow>(profile.Sample.Count);
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

            samples.Add(new ProfileSampleRow
            {
                ProfileId = profileId,
                Ordinal = i,
                StackOrdinal = s.StackIndex,
                LinkTraceId = linkTraceId,
                LinkSpanId = linkSpanId,
                ValuesJson = s.Values.Count > 0 ? JsonSerializer.Serialize(s.Values) : null,
                TimestampsJson = s.TimestampsUnixNano.Count > 0
                    ? JsonSerializer.Serialize(s.TimestampsUnixNano)
                    : null
            });
        }

        var stacks = new List<ProfileStackRow>(dictionary.StackTable.Count);
        for (var i = 0; i < dictionary.StackTable.Count; i++)
        {
            var st = dictionary.StackTable[i];
            stacks.Add(new ProfileStackRow
            {
                ProfileId = profileId,
                Ordinal = i,
                LocationOrdinalsJson = st.LocationIndices.Count > 0
                    ? JsonSerializer.Serialize(st.LocationIndices)
                    : null
            });
        }

        return new ProfileConversionResult
        {
            Profile = header,
            Functions = functions,
            Locations = locations,
            Mappings = mappings,
            Samples = samples,
            Stacks = stacks
        };
    }

    private static Dictionary<string, string> ExtractProfileAttributes(
        RepeatedField<int> indices,
        ProtoProfilesDictionary dictionary)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var index in indices)
        {
            if (index < 0 || index >= dictionary.AttributeTable.Count)
                continue;

            var attribute = dictionary.AttributeTable[index];
            var key = Resolve(attribute.KeyStrindex, dictionary);
            if (string.IsNullOrEmpty(key))
                continue;

            var value = ConvertProtoAnyValueToString(attribute.Value);
            if (value is not null)
                attributes[key] = value;
        }

        return attributes;
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

internal readonly record struct ProfileLocationLineJson(
    [property: JsonPropertyName("functionOrdinal")] int FunctionOrdinal,
    [property: JsonPropertyName("line")] long Line,
    [property: JsonPropertyName("column")] long Column);
