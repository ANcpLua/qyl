
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using ProtoAnyValue = OpenTelemetry.Proto.Common.V1.AnyValue;
using ProtoKeyValue = OpenTelemetry.Proto.Common.V1.KeyValue;
using ProtoLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;
using ProtoResource = OpenTelemetry.Proto.Resource.V1.Resource;
using ProtoSpan = OpenTelemetry.Proto.Trace.V1.Span;
using Qyl.Collector.Services;
using Qyl.Collector.Primitives;

namespace Qyl.Collector.Ingestion;

public static class OtlpConverter
{
    private static readonly LogSourceEnricher s_logSourceEnricher =
        new(new SourceLocationCache(), new PdbSourceResolver());

    #region OTLP Trace Conversion

    public static List<SpanStorageRow> ConvertTraceRequestToStorageRows(ExportTraceServiceRequest request)
    {
        var spans = new List<SpanStorageRow>();

        foreach (var resourceSpan in request.ResourceSpans)
        {
            var serviceName = ExtractServiceNameFromProto(resourceSpan.Resource);
            var resourceAttrs = ExtractResourceAttributesFromProto(resourceSpan.Resource);
            var resourceJson = resourceAttrs.Count > 0
                ? JsonSerializer.Serialize(resourceAttrs, QylSerializerContext.Default.DictionaryStringString)
                : null;
            var schemaUrl = resourceSpan.SchemaUrl;

            foreach (var scopeSpan in resourceSpan.ScopeSpans)
            {
                var effectiveSchemaUrl = !string.IsNullOrEmpty(scopeSpan.SchemaUrl)
                    ? scopeSpan.SchemaUrl
                    : schemaUrl;

                foreach (var span in scopeSpan.Spans)
                {
                    var attributes = ExtractAttributesFromProto(span.Attributes, serviceName);
                    var baggageJson = ExtractBaggageJson(attributes);
                    var spanRecord =
                        CreateStorageRowFromProto(span, serviceName, attributes, baggageJson, effectiveSchemaUrl,
                            resourceJson);
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
                    Key: SemanticAttributeKeys.ServiceName,
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
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal) { [SemanticAttributeKeys.ServiceName] = serviceName };

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
            ProtoAnyValue.ValueOneofCase.BytesValue => Convert.ToBase64String(value.BytesValue.ToByteArray()),
            ProtoAnyValue.ValueOneofCase.ArrayValue => SerializeProtoArray(value.ArrayValue.Values),
            ProtoAnyValue.ValueOneofCase.KvlistValue => SerializeProtoKeyValueList(value.KvlistValue.Values),
            _ => null
        };
    }

    private static SpanStorageRow CreateStorageRowFromProto(
        ProtoSpan span,
        string serviceName,
        Dictionary<string, string> attributes,
        string? baggageJson,
        string? schemaUrl,
        string? resourceJson) =>
        CreateStorageRow(
            ToHex(span.SpanId), ToHex(span.TraceId), ToHex(span.ParentSpanId), span.Name,
            (int)span.Kind, span.StartTimeUnixNano, span.EndTimeUnixNano,
            span.Status is not null ? (int)span.Status.Code : null, span.Status?.Message,
            serviceName, attributes, baggageJson, schemaUrl, resourceJson);

    #endregion

    #region Shared Helpers

    private static SpanStorageRow CreateStorageRow(
        string? spanId, string? traceId, string? parentSpanId, string? name,
        int? kind, ulong startNano, ulong endNano,
        int? statusCode, string? statusMessage,
        string serviceName, Dictionary<string, string> attributes,
        string? baggageJson, string? schemaUrl, string? resourceJson)
    {
        var durationNs = endNano >= startNano ? endNano - startNano : 0UL;
        var genAi = ExtractGenAiAttributes(attributes);

        return new SpanStorageRow
        {
            SpanId = spanId ?? "",
            TraceId = traceId ?? "",
            ParentSpanId = string.IsNullOrEmpty(parentSpanId) ? null : parentSpanId,
            SessionId = attributes.GetFirstValueOrDefault(SemanticAttributeKeys.SessionCorrelationKeys),
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
            AttributesJson =
                JsonSerializer.Serialize(attributes, QylSerializerContext.Default.DictionaryStringString),
            ResourceJson = resourceJson,
            BaggageJson = baggageJson,
            SchemaUrl = schemaUrl
        };
    }

    public static ServiceInstanceRecord? ExtractServiceInstance(
        IReadOnlyDictionary<string, string> resourceAttributes,
        IReadOnlyDictionary<string, string>? spanAttributes,
        ulong timestampNano)
    {
        if (!resourceAttributes.TryGetValue(SemanticAttributeKeys.ServiceName, out var serviceName) ||
            serviceName is "unknown" or "")
            return null;

        var serviceType = ServiceClassifier.Classify(resourceAttributes, spanAttributes);

        string? metadataJson = null;
        Dictionary<string, string>? capabilities = null;
        foreach (var (key, value) in resourceAttributes)
        {
            if (!key.StartsWithOrdinal(SemanticAttributeKeys.QylCapabilityPrefix) || string.IsNullOrEmpty(value))
                continue;
            capabilities ??= new Dictionary<string, string>(StringComparer.Ordinal);
            capabilities[key] = value;
        }

        if (capabilities is not null)
            metadataJson = JsonSerializer.Serialize(capabilities, QylSerializerContext.Default.DictionaryStringString);

        return new ServiceInstanceRecord
        {
            ServiceNamespace = resourceAttributes.GetValueOrDefault(SemanticAttributeKeys.ServiceNamespace) ?? "",
            ServiceName = serviceName,
            ServiceInstanceId = resourceAttributes.GetValueOrDefault(SemanticAttributeKeys.ServiceInstanceId)
                                ?? Environment.MachineName,
            ServiceType = serviceType,
            ServiceVersion = resourceAttributes.GetValueOrDefault(SemanticAttributeKeys.ServiceVersion),
            DeploymentEnvironment = resourceAttributes.GetValueOrDefault(SemanticAttributeKeys.DeploymentEnvironmentName),
            OsType = resourceAttributes.GetValueOrDefault(SemanticAttributeKeys.OsType),
            HostArch = resourceAttributes.GetValueOrDefault(SemanticAttributeKeys.HostArch),
            AgentName = resourceAttributes.GetValueOrDefault(SemanticAttributeKeys.GenAiAgentName),
            ProviderName = resourceAttributes.GetValueOrDefault(SemanticAttributeKeys.GenAiProviderName),
            DefaultModel = resourceAttributes.GetValueOrDefault(SemanticAttributeKeys.GenAiRequestModel),
            TimestampNano = timestampNano,
            MetadataJson = metadataJson
        };
    }

    public static List<ServiceInstanceRecord> ExtractServiceInstances(ExportTraceServiceRequest request)
    {
        var instances = new List<ServiceInstanceRecord>();

        foreach (var resourceSpan in request.ResourceSpans)
        {
            var resourceAttrs = ExtractResourceAttributesFromProto(resourceSpan.Resource);

            ProtoSpan? firstSpan = null;
            foreach (var scopeSpan in resourceSpan.ScopeSpans)
            {
                if (scopeSpan.Spans.Count > 0)
                {
                    firstSpan = scopeSpan.Spans[0];
                    break;
                }
            }

            Dictionary<string, string>? spanAttrs = null;
            ulong timestamp = 0;

            if (firstSpan is not null)
            {
                spanAttrs = ExtractAttributesFromProto(firstSpan.Attributes,
                    resourceAttrs.GetValueOrDefault(SemanticAttributeKeys.ServiceName) ?? "unknown");
                timestamp = firstSpan.StartTimeUnixNano;
            }

            if (timestamp is 0)
                timestamp = QylTimeConversions.ToUnixNanoUnsigned(TimeProvider.System.GetUtcNow());

            var instance = ExtractServiceInstance(resourceAttrs, spanAttrs, timestamp);
            if (instance is not null)
                instances.Add(instance);
        }

        return instances;
    }

    private static string? ConvertJsonValueToString(OtlpAnyValue? value)
    {
        if (value is null)
            return null;

        if (value.StringValue is not null)
            return value.StringValue;
        if (value.IntValue.HasValue)
            return value.IntValue.Value.ToString();
        if (value.DoubleValue.HasValue)
            return value.DoubleValue.Value.ToString(CultureInfo.InvariantCulture);
        if (value.BoolValue.HasValue)
            return value.BoolValue.Value.ToString().ToLowerInvariant();
        if (value.BytesValue is not null)
            return value.BytesValue;

        if (value.ArrayValue is not null)
        {
            var items = value.ArrayValue.Values
                            ?.Select(ConvertJsonValueToString)
                            .Where(static (string? v) => v is not null)
                            .ToArray() ??
                        [];

            return JsonSerializer.Serialize(items, QylSerializerContext.Default.StringArray);
        }

        if (value.KvlistValue is null)
            return null;

        var dict = value.KvlistValue.Values
                       ?.Where(static kv => ConvertJsonValueToString(kv.Value) is not null)
                       .ToDictionary(static kv => kv.Key ?? "", static kv => ConvertJsonValueToString(kv.Value) ?? "")
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);

        return JsonSerializer.Serialize(dict, QylSerializerContext.Default.DictionaryStringString);
    }

    private static string SerializeProtoArray(RepeatedField<ProtoAnyValue> values)
    {
        var items = values.Select(ConvertProtoAnyValueToString)
            .Where(static (string? v) => v is not null)
            .ToArray();

        return JsonSerializer.Serialize(items, QylSerializerContext.Default.StringArray);
    }

    private static string SerializeProtoKeyValueList(RepeatedField<ProtoKeyValue> values)
    {
        var dict = values
            .Where(static kv => ConvertProtoAnyValueToString(kv.Value) is not null)
            .ToDictionary(static kv => kv.Key ?? "", static kv => ConvertProtoAnyValueToString(kv.Value) ?? "");

        return JsonSerializer.Serialize(dict, QylSerializerContext.Default.DictionaryStringString);
    }

    private static string? ToHex(ByteString value)
    {
        if (value.Length is 0) return null;
        return Convert.ToHexString(value.ToByteArray()).ToLowerInvariant();
    }

    private static string? ExtractBaggageJson(IReadOnlyDictionary<string, string> attributes)
    {
        Dictionary<string, string>? baggage = null;

        foreach (var kvp in attributes)
        {
            if (!kvp.Key.StartsWithOrdinal("baggage."))
                continue;

            var baggageKey = kvp.Key[8..];
            if (string.IsNullOrEmpty(baggageKey))
                continue;

            baggage ??= new Dictionary<string, string>(StringComparer.Ordinal);
            baggage[baggageKey] = kvp.Value;
        }

        if (baggage is null || baggage.Count is 0)
            return null;

        return JsonSerializer.Serialize(baggage, QylSerializerContext.Default.DictionaryStringString);
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
        var providerName = attributes.GetValueOrDefault(SemanticAttributeKeys.GenAiProviderName);

        var requestModel = attributes.GetValueOrDefault(SemanticAttributeKeys.GenAiRequestModel);
        var responseModel = attributes.GetValueOrDefault(SemanticAttributeKeys.GenAiResponseModel);

        var tokensIn = ParseNullableLong(
            attributes.GetValueOrDefault(SemanticAttributeKeys.GenAiUsageInputTokens));

        var tokensOut = ParseNullableLong(
            attributes.GetValueOrDefault(SemanticAttributeKeys.GenAiUsageOutputTokens));

        var temperature = ParseNullableDouble(attributes.GetValueOrDefault(SemanticAttributeKeys.GenAiRequestTemperature));
        var stopReason = attributes.GetValueOrDefault(SemanticAttributeKeys.GenAiResponseFinishReasons);
        var toolName = attributes.GetValueOrDefault(SemanticAttributeKeys.GenAiToolName);
        var toolCallId = attributes.GetValueOrDefault(SemanticAttributeKeys.GenAiToolCallId);
        var costUsd = ParseNullableDouble(attributes.GetValueOrDefault(SemanticAttributeKeys.GenAiCostUsd));

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
            var resourceJson = resourceAttrs.Count > 0
                ? JsonSerializer.Serialize(resourceAttrs, QylSerializerContext.Default.DictionaryStringString)
                : null;

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
        var sessionId = log.Attributes
            .FirstOrDefault(static a => a.Key.IsAny(SemanticAttributeKeys.SessionCorrelationKeys))
            is { Value.ValueCase: ProtoAnyValue.ValueOneofCase.StringValue } sessionAttr
            ? sessionAttr.Value.StringValue
            : null;

        var body = ConvertProtoAnyValueToString(log.Body);

        var severityNumber = (int)log.SeverityNumber;
        var severityText = string.IsNullOrEmpty(log.SeverityText)
            ? SeverityNumberToText(severityNumber)
            : log.SeverityText;
        var sourceLocation = s_logSourceEnricher.Enrich(log.Attributes);

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
            ResourceJson = resourceJson,
            SourceFile = sourceLocation?.FilePath,
            SourceLine = sourceLocation?.Line,
            SourceColumn = sourceLocation?.Column,
            SourceMethod = sourceLocation?.MethodName
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

        return JsonSerializer.Serialize(dict, QylSerializerContext.Default.DictionaryStringString);
    }

    private static string? SerializeAttributes(List<OtlpKeyValue>? attributes)
    {
        if (attributes is null || attributes.Count is 0) return null;

        var dict = new Dictionary<string, string>(attributes.Count);
        foreach (var attr in attributes)
        {
            if (attr.Key is null) continue;
            dict[attr.Key] = ConvertJsonValueToString(attr.Value) ?? "";
        }

        return JsonSerializer.Serialize(dict, QylSerializerContext.Default.DictionaryStringString);
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

    public static List<ProfileConversionResult> ConvertProfilesToNormalizedRows(OtlpExportProfilesServiceRequest otlp)
    {
        var results = new List<ProfileConversionResult>();

        foreach (var resourceProfiles in otlp.ResourceProfiles ?? [])
        {
            var serviceName = resourceProfiles.Resource?.Attributes?
                                  .FirstOrDefault(static a => a.Key == SemanticAttributeKeys.ServiceName)?.Value?.StringValue
                              ?? "unknown";

            var resourceJson = SerializeAttributes(resourceProfiles.Resource?.Attributes);
            var schemaUrl = resourceProfiles.SchemaUrl;

            foreach (var scopeProfiles in resourceProfiles.ScopeProfiles ?? [])
            {
                var effectiveSchemaUrl = !string.IsNullOrEmpty(scopeProfiles.SchemaUrl)
                    ? scopeProfiles.SchemaUrl
                    : schemaUrl;

                foreach (var profile in scopeProfiles.Profiles ?? [])
                {
                    results.Add(CreateNormalizedProfile(profile, serviceName, resourceJson, effectiveSchemaUrl));
                }
            }
        }

        return results;
    }

    private static ProfileConversionResult CreateNormalizedProfile(
        OtlpProfile profile,
        string serviceName,
        string? resourceJson,
        string? schemaUrl)
    {
        var profileId = profile.ProfileId ?? Guid.NewGuid().ToString("N")[..16];
        var strings = profile.StringTable ?? [];

        var sessionId = profile.Attributes?
            .FirstOrDefault(static a =>
                a.Key is not null && a.Key.IsAny(SemanticAttributeKeys.SessionCorrelationKeys))
            ?.Value?.StringValue;

        var profileFrameType = profile.Attributes?
            .FirstOrDefault(static a => a.Key == "profile.frame.type")?.Value?.StringValue;

        string? traceId = null;
        string? spanId = null;
        if (profile.LinkTable is { Count: > 0 })
        {
            traceId = profile.LinkTable[0].TraceId;
            spanId = profile.LinkTable[0].SpanId;
        }

        var sampleType = Resolve(profile.SampleType?.TypeStrindex);
        var sampleUnit = Resolve(profile.SampleType?.UnitStrindex);

        var header = new ProfileStorageRow
        {
            ProfileId = profileId,
            TraceId = traceId,
            SpanId = spanId,
            SessionId = sessionId,
            TimeUnixNano = profile.TimeUnixNano,
            DurationNano = profile.DurationNano,
            SampleCount = profile.Samples?.Count ?? 0,
            SampleType = sampleType,
            SampleUnit = sampleUnit,
            OriginalPayloadFormat = profile.OriginalPayloadFormat,
            ServiceName = serviceName,
            ProfileFrameType = profileFrameType,
            AttributesJson = SerializeAttributes(profile.Attributes),
            ResourceJson = resourceJson,
            ProfileDataJson = JsonSerializer.Serialize(profile, QylSerializerContext.Default.OtlpProfile),
            SchemaUrl = schemaUrl
        };

        var functions = new List<ProfileFunctionRow>();
        foreach (var (f, i) in (profile.FunctionTable ?? []).Select(static (f, i) => (f, i)))
        {
            functions.Add(new ProfileFunctionRow
            {
                ProfileId = profileId,
                Ordinal = i,
                Name = Resolve(f.NameStrindex),
                SystemName = Resolve(f.SystemNameStrindex),
                Filename = Resolve(f.FilenameStrindex),
                StartLine = f.StartLine
            });
        }

        var locations = new List<ProfileLocationRow>();
        foreach (var (loc, i) in (profile.LocationTable ?? []).Select(static (l, i) => (l, i)))
        {
            string? linesJson = null;
            if (loc.Lines is { Count: > 0 })
            {
                linesJson = JsonSerializer.Serialize(
                    loc.Lines.Select(static line => new
                    {
                        functionOrdinal = line.FunctionIndex, line = line.Line, column = line.Column
                    }));
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

        var mappings = new List<ProfileMappingRow>();
        foreach (var (m, i) in (profile.MappingTable ?? []).Select(static (m, i) => (m, i)))
        {
            mappings.Add(new ProfileMappingRow
            {
                ProfileId = profileId,
                Ordinal = i,
                Filename = Resolve(m.FilenameStrindex),
                MemoryStart = m.MemoryStart,
                MemoryLimit = m.MemoryLimit,
                FileOffset = m.FileOffset
            });
        }

        var samples = new List<ProfileSampleRow>();
        foreach (var (s, i) in (profile.Samples ?? []).Select(static (s, i) => (s, i)))
        {
            string? linkTraceId = null;
            string? linkSpanId = null;
            if (s.LinkIndex is { } li && profile.LinkTable is not null && li >= 0 && li < profile.LinkTable.Count)
            {
                linkTraceId = profile.LinkTable[li].TraceId;
                linkSpanId = profile.LinkTable[li].SpanId;
            }

            samples.Add(new ProfileSampleRow
            {
                ProfileId = profileId,
                Ordinal = i,
                StackOrdinal = s.StackIndex,
                LinkTraceId = linkTraceId,
                LinkSpanId = linkSpanId,
                ValuesJson = s.Values is { Count: > 0 } ? JsonSerializer.Serialize(s.Values) : null,
                TimestampsJson = s.TimestampsUnixNano is { Count: > 0 }
                    ? JsonSerializer.Serialize(s.TimestampsUnixNano)
                    : null
            });
        }

        var stacks = new List<ProfileStackRow>();
        foreach (var (st, i) in (profile.StackTable ?? []).Select(static (st, i) => (st, i)))
        {
            stacks.Add(new ProfileStackRow
            {
                ProfileId = profileId,
                Ordinal = i,
                LocationOrdinalsJson = st.LocationIndices is { Count: > 0 }
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

        string? Resolve(int? index) =>
            index is { } i and >= 0 && i < strings.Count ? strings[i] : null;
    }

    #endregion
}
