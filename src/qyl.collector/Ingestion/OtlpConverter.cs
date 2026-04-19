// =============================================================================
// qyl OTLP Conversion - Single Source of Truth for OTLP → SpanStorageRow
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.40.0
// =============================================================================

using Qyl.Collector.Grpc;
using Qyl.Collector.Services;
using Qyl.Contracts.Primitives;

namespace Qyl.Collector.Ingestion;

/// <summary>
///     Converts OTLP protobuf/JSON to SpanStorageRow.
///     Single source of truth for both gRPC (port 4317) and HTTP (POST /v1/traces) endpoints.
/// </summary>
public static class OtlpConverter
{
    private static readonly LogSourceEnricher SLogSourceEnricher =
        new(new SourceLocationCache(), new PdbSourceResolver());

    #region Proto Conversion (gRPC endpoint)

    /// <summary>
    ///     Converts gRPC OTLP ExportTraceServiceRequest (proto) to storage rows.
    ///     Used by: TraceServiceImpl (gRPC :4317)
    /// </summary>
    public static List<SpanStorageRow> ConvertProtoToStorageRows(ExportTraceServiceRequest request)
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
                // Use scope schema URL if available, otherwise resource-level
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

    private static string ExtractServiceNameFromProto(OtlpResourceProto? resource)
    {
        if (resource?.Attributes is null) return "unknown";

        foreach (var attr in resource.Attributes)
        {
            if (attr is { Key: "service.name", Value.StringValue: not null })
                return attr.Value.StringValue;
        }

        return "unknown";
    }

    private static Dictionary<string, string> ExtractResourceAttributesFromProto(OtlpResourceProto? resource)
    {
        var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (resource?.Attributes is null) return attrs;

        foreach (var attr in resource.Attributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;
            var value = ConvertAnyValueToString(attr.Value);
            if (value is not null) attrs[attr.Key] = value;
        }

        return attrs;
    }

    private static Dictionary<string, string> ExtractAttributesFromProto(
        List<OtlpKeyValueProto>? protoAttributes,
        string serviceName)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal) { ["service.name"] = serviceName };

        if (protoAttributes is null) return attributes;

        foreach (var attr in protoAttributes)
        {
            if (string.IsNullOrEmpty(attr.Key)) continue;

            var value = ConvertAnyValueToString(attr.Value);
            if (value is not null) attributes[attr.Key] = value;
        }

        return attributes;
    }

    private static string? ConvertAnyValueToString(OtlpAnyValueProto? value)
    {
        if (value is null) return null;

        // Priority order matches protobuf oneof semantics
        if (value.StringValue is not null) return value.StringValue;
        if (value.IntValue.HasValue) return value.IntValue.Value.ToString();
        if (value.DoubleValue.HasValue) return value.DoubleValue.Value.ToString(CultureInfo.InvariantCulture);
        if (value.BoolValue.HasValue) return value.BoolValue.Value.ToString().ToLowerInvariant();
        if (value.BytesValue is not null) return Convert.ToBase64String(value.BytesValue);
        if (value.ArrayValue is not null)
        {
            var items = value.ArrayValue.Select(ConvertAnyValueToString).Where(static (string? v) => v is not null)
                .ToArray();
            return JsonSerializer.Serialize(items, QylSerializerContext.Default.StringArray);
        }

        if (value.KvlistValue is null) return null;
        var dict = value.KvlistValue
            .Where(static kv => ConvertAnyValueToString(kv.Value) is not null)
            .ToDictionary(static kv => kv.Key ?? "", static kv => ConvertAnyValueToString(kv.Value));
        return JsonSerializer.Serialize(dict, QylSerializerContext.Default.DictionaryStringString);
    }

    private static SpanStorageRow CreateStorageRowFromProto(
        OtlpSpanProto span,
        string serviceName,
        Dictionary<string, string> attributes,
        string? baggageJson,
        string? schemaUrl,
        string? resourceJson) =>
        CreateStorageRow(
            span.SpanId, span.TraceId, span.ParentSpanId, span.Name,
            span.Kind, span.StartTimeUnixNano, span.EndTimeUnixNano,
            span.Status?.Code, span.Status?.Message,
            serviceName, attributes, baggageJson, schemaUrl, resourceJson);

    #endregion

    #region JSON Conversion (HTTP endpoint)

    /// <summary>
    ///     Converts HTTP OTLP JSON request to storage rows.
    ///     Used by: Program.cs (POST /v1/traces)
    /// </summary>
    public static List<SpanStorageRow> ConvertJsonToStorageRows(OtlpExportTraceServiceRequest otlp)
    {
        var spans = new List<SpanStorageRow>();

        foreach (var resourceSpan in otlp.ResourceSpans ?? [])
        {
            var serviceName = resourceSpan.Resource?.Attributes?
                                  .FirstOrDefault(static a => a.Key == "service.name")?.Value?.StringValue
                              ?? "unknown";
            var resourceJson = SerializeAttributes(resourceSpan.Resource?.Attributes);
            var schemaUrl = resourceSpan.SchemaUrl;

            foreach (var scopeSpan in resourceSpan.ScopeSpans ?? [])
            {
                // Use scope schema URL if available, otherwise resource-level
                var effectiveSchemaUrl = !string.IsNullOrEmpty(scopeSpan.SchemaUrl)
                    ? scopeSpan.SchemaUrl
                    : schemaUrl;

                foreach (var span in scopeSpan.Spans ?? [])
                {
                    var attributes = ExtractAttributesFromJson(span.Attributes, serviceName);
                    var baggageJson = ExtractBaggageJson(attributes);
                    spans.Add(CreateStorageRowFromJson(span, serviceName, attributes, baggageJson, effectiveSchemaUrl,
                        resourceJson));
                }
            }
        }

        return spans;
    }

    private static Dictionary<string, string> ExtractAttributesFromJson(
        List<OtlpKeyValue>? jsonAttributes,
        string serviceName)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal) { ["service.name"] = serviceName };

        if (jsonAttributes is null) return attributes;

        foreach (var attr in jsonAttributes)
        {
            if (attr.Key is null) continue;

            var value = ConvertJsonValueToString(attr.Value);
            if (value is not null) attributes[attr.Key] = value;
        }

        return attributes;
    }

    private static SpanStorageRow CreateStorageRowFromJson(
        OtlpSpan span,
        string serviceName,
        Dictionary<string, string> attributes,
        string? baggageJson,
        string? schemaUrl,
        string? resourceJson) =>
        CreateStorageRow(
            span.SpanId, span.TraceId, span.ParentSpanId, span.Name,
            span.Kind, span.StartTimeUnixNano, span.EndTimeUnixNano,
            span.Status?.Code, span.Status?.Message,
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
            SessionId = attributes.GetValueOrDefault("session.id"),
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

    /// <summary>
    ///     Extracts a ServiceInstanceRecord from OTLP resource attributes.
    ///     Returns null if service.name is "unknown" or missing.
    /// </summary>
    public static ServiceInstanceRecord? ExtractServiceInstance(
        IReadOnlyDictionary<string, string> resourceAttributes,
        IReadOnlyDictionary<string, string>? spanAttributes,
        ulong timestampNano)
    {
        if (!resourceAttributes.TryGetValue("service.name", out var serviceName) ||
            serviceName is "unknown" or "")
            return null;

        var serviceType = ServiceClassifier.Classify(resourceAttributes, spanAttributes);

        // Extract compile-time capability manifest (qyl.capability.* Resource attributes)
        string? metadataJson = null;
        Dictionary<string, string>? capabilities = null;
        foreach (var (key, value) in resourceAttributes)
        {
            if (!key.StartsWithOrdinal("qyl.capability.") || string.IsNullOrEmpty(value))
                continue;
            capabilities ??= new Dictionary<string, string>(StringComparer.Ordinal);
            capabilities[key] = value;
        }

        if (capabilities is not null)
            metadataJson = JsonSerializer.Serialize(capabilities, QylSerializerContext.Default.DictionaryStringString);

        return new ServiceInstanceRecord
        {
            ServiceNamespace = resourceAttributes.GetValueOrDefault("service.namespace") ?? "",
            ServiceName = serviceName,
            ServiceInstanceId = resourceAttributes.GetValueOrDefault("service.instance.id")
                                ?? Environment.MachineName,
            ServiceType = serviceType,
            ServiceVersion = resourceAttributes.GetValueOrDefault("service.version"),
            DeploymentEnvironment = resourceAttributes.GetValueOrDefault("deployment.environment.name")
                                    ?? resourceAttributes.GetValueOrDefault("deployment.environment"),
            OsType = resourceAttributes.GetValueOrDefault("os.type"),
            HostArch = resourceAttributes.GetValueOrDefault("host.arch"),
            AgentName = resourceAttributes.GetValueOrDefault("gen_ai.agent.name"),
            ProviderName = resourceAttributes.GetValueOrDefault("gen_ai.provider.name"),
            DefaultModel = resourceAttributes.GetValueOrDefault("gen_ai.request.model"),
            TimestampNano = timestampNano,
            MetadataJson = metadataJson
        };
    }

    /// <summary>
    ///     Extracts service instances from an OTLP JSON trace request.
    ///     One record per unique resource in the request.
    /// </summary>
    public static List<ServiceInstanceRecord> ExtractServiceInstancesFromJson(OtlpExportTraceServiceRequest otlp)
    {
        var instances = new List<ServiceInstanceRecord>();

        foreach (var resourceSpan in otlp.ResourceSpans ?? [])
        {
            var resourceAttrs = new Dictionary<string, string>(StringComparer.Ordinal);
            if (resourceSpan.Resource?.Attributes is not null)
            {
                foreach (var attr in resourceSpan.Resource.Attributes)
                {
                    if (string.IsNullOrEmpty(attr.Key)) continue;
                    var value = ConvertJsonValueToString(attr.Value);
                    if (value is not null) resourceAttrs[attr.Key] = value;
                }
            }

            // Use first span's attributes for classification
            var firstSpan = resourceSpan.ScopeSpans?.FirstOrDefault()?.Spans?.FirstOrDefault();
            Dictionary<string, string>? spanAttrs = null;
            ulong timestamp = 0;

            if (firstSpan is not null)
            {
                spanAttrs = ExtractAttributesFromJson(firstSpan.Attributes,
                    resourceAttrs.GetValueOrDefault("service.name") ?? "unknown");
                timestamp = firstSpan.StartTimeUnixNano;
            }

            if (timestamp is 0)
                timestamp = TimeConversions.ToUnixNanoUnsigned(TimeProvider.System.GetUtcNow());

            var instance = ExtractServiceInstance(resourceAttrs, spanAttrs, timestamp);
            if (instance is not null)
                instances.Add(instance);
        }

        return instances;
    }

    /// <summary>
    ///     Extracts service instances from an OTLP proto trace request.
    ///     One record per unique resource in the request.
    /// </summary>
    public static List<ServiceInstanceRecord> ExtractServiceInstancesFromProto(ExportTraceServiceRequest request)
    {
        var instances = new List<ServiceInstanceRecord>();

        foreach (var resourceSpan in request.ResourceSpans)
        {
            var resourceAttrs = ExtractResourceAttributesFromProto(resourceSpan.Resource);

            // Use first span's attributes for classification
            OtlpSpanProto? firstSpan = null;
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
                    resourceAttrs.GetValueOrDefault("service.name") ?? "unknown");
                timestamp = firstSpan.StartTimeUnixNano;
            }

            if (timestamp is 0)
                timestamp = TimeConversions.ToUnixNanoUnsigned(TimeProvider.System.GetUtcNow());

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

    /// <summary>
    ///     Extracts baggage from attributes prefixed with "baggage." and serializes to JSON.
    ///     OTLP doesn't have a dedicated baggage field, so some instrumentations
    ///     store baggage in span attributes with "baggage." prefix.
    /// </summary>
    private static string? ExtractBaggageJson(IReadOnlyDictionary<string, string> attributes)
    {
        Dictionary<string, string>? baggage = null;

        foreach (var kvp in attributes)
        {
            if (!kvp.Key.StartsWithOrdinal("baggage."))
                continue;

            var baggageKey = kvp.Key[8..]; // Remove "baggage." prefix
            if (string.IsNullOrEmpty(baggageKey))
                continue;

            baggage ??= new Dictionary<string, string>(StringComparer.Ordinal);
            baggage[baggageKey] = kvp.Value;
        }

        if (baggage is null || baggage.Count is 0)
            return null;

        return JsonSerializer.Serialize(baggage, QylSerializerContext.Default.DictionaryStringString);
    }

    /// <summary>
    ///     GenAI attribute extraction result.
    /// </summary>
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

    /// <summary>
    ///     Extracts GenAI attributes with fallback to deprecated OTel 1.38 names.
    /// </summary>
    private static GenAiData ExtractGenAiAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        var providerName = attributes.GetValueOrDefault("gen_ai.provider.name")
                           ?? attributes.GetValueOrDefault("gen_ai.system");

        var requestModel = attributes.GetValueOrDefault("gen_ai.request.model");
        var responseModel = attributes.GetValueOrDefault("gen_ai.response.model");

        var tokensIn = ParseNullableLong(
            attributes.GetValueOrDefault("gen_ai.usage.input_tokens")
            ?? attributes.GetValueOrDefault("gen_ai.usage.prompt_tokens"));

        var tokensOut = ParseNullableLong(
            attributes.GetValueOrDefault("gen_ai.usage.output_tokens")
            ?? attributes.GetValueOrDefault("gen_ai.usage.completion_tokens"));

        var temperature = ParseNullableDouble(attributes.GetValueOrDefault("gen_ai.request.temperature"));
        var stopReason = attributes.GetValueOrDefault("gen_ai.response.finish_reasons")
                         ?? attributes.GetValueOrDefault("gen_ai.response.finish_reason");
        var toolName = attributes.GetValueOrDefault("gen_ai.tool.name");
        var toolCallId = attributes.GetValueOrDefault("gen_ai.tool.call.id");
        var costUsd = ParseNullableDouble(attributes.GetValueOrDefault("gen_ai.usage.cost"));

        return new GenAiData(
            providerName, requestModel, responseModel,
            tokensIn, tokensOut, temperature, stopReason,
            toolName, toolCallId, costUsd);
    }

    /// <summary>
    ///     Converts SpanKind int to byte (TINYINT in DuckDB).
    /// </summary>
    private static byte ConvertSpanKindToByte(int? kind) => kind switch
    {
        1 => 1, // INTERNAL
        2 => 2, // SERVER
        3 => 3, // CLIENT
        4 => 4, // PRODUCER
        5 => 5, // CONSUMER
        _ => 0 // UNSPECIFIED
    };

    /// <summary>
    ///     Converts StatusCode int to byte (TINYINT in DuckDB).
    /// </summary>
    private static byte ConvertStatusCodeToByte(int? code) => code switch
    {
        1 => 1, // OK
        2 => 2, // ERROR
        _ => 0 // UNSET
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

    /// <summary>
    ///     Converts HTTP OTLP JSON log request to storage rows.
    ///     Used by: Program.cs (POST /v1/logs)
    /// </summary>
    public static List<LogStorageRow> ConvertLogsToStorageRows(OtlpExportLogsServiceRequest otlp)
    {
        var logs = new List<LogStorageRow>();

        foreach (var resourceLogs in otlp.ResourceLogs ?? [])
        {
            var serviceName = resourceLogs.Resource?.Attributes?
                                  .FirstOrDefault(static a => a.Key == "service.name")?.Value?.StringValue
                              ?? "unknown";

            var resourceJson = SerializeAttributes(resourceLogs.Resource?.Attributes);

            foreach (var scopeLogs in resourceLogs.ScopeLogs ?? [])
            foreach (var log in scopeLogs.LogRecords ?? [])
            {
                logs.Add(CreateLogStorageRow(log, serviceName, resourceJson));
            }
        }

        return logs;
    }

    private static LogStorageRow CreateLogStorageRow(
        OtlpLogRecord log,
        string serviceName,
        string? resourceJson)
    {
        var sessionId = log.Attributes?
            .FirstOrDefault(static a => a.Key == "session.id")?.Value?.StringValue;

        var body = log.Body?.StringValue
                   ?? log.Body?.IntValue?.ToString()
                   ?? log.Body?.DoubleValue?.ToString(CultureInfo.InvariantCulture)
                   ?? log.Body?.BoolValue?.ToString();

        var severityNumber = log.SeverityNumber ?? 0;
        var severityText = log.SeverityText ?? SeverityNumberToText(severityNumber);
        var sourceLocation = SLogSourceEnricher.Enrich(log);

        return new LogStorageRow
        {
            LogId = Guid.NewGuid().ToString("N"),
            TraceId = string.IsNullOrEmpty(log.TraceId) ? null : log.TraceId,
            SpanId = string.IsNullOrEmpty(log.SpanId) ? null : log.SpanId,
            SessionId = sessionId,
            TimeUnixNano = log.TimeUnixNano, // ulong - store directly
            ObservedTimeUnixNano = log.ObservedTimeUnixNano > 0 ? log.ObservedTimeUnixNano : null,
            SeverityNumber = (byte)Math.Clamp(severityNumber, 0, 24), // TINYINT (byte)
            SeverityText = severityText,
            Body = body,
            ServiceName = serviceName,
            AttributesJson = SerializeAttributes(log.Attributes),
            ResourceJson = resourceJson,
            SourceFile = sourceLocation?.FilePath,
            SourceLine = sourceLocation?.Line,
            SourceColumn = sourceLocation?.Column,
            SourceMethod = sourceLocation?.MethodName
        };
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

    /// <summary>
    ///     Converts HTTP OTLP JSON profiles request to normalized storage rows.
    ///     Dereferences all string table indices into resolved strings.
    ///     Used by: CollectorEndpointExtensions (POST /v1/profiles)
    /// </summary>
    public static List<ProfileConversionResult> ConvertProfilesToNormalizedRows(OtlpExportProfilesServiceRequest otlp)
    {
        var results = new List<ProfileConversionResult>();

        foreach (var resourceProfiles in otlp.ResourceProfiles ?? [])
        {
            var serviceName = resourceProfiles.Resource?.Attributes?
                                  .FirstOrDefault(static a => a.Key == "service.name")?.Value?.StringValue
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

        string? Resolve(int? index) =>
            index is { } i and >= 0 && i < strings.Count ? strings[i] : null;

        var sessionId = profile.Attributes?
            .FirstOrDefault(static a => a.Key == "session.id")?.Value?.StringValue;

        var profileFrameType = profile.Attributes?
            .FirstOrDefault(static a => a.Key == "profile.frame.type")?.Value?.StringValue;

        // Cross-signal correlation from first Link
        string? traceId = null;
        string? spanId = null;
        if (profile.LinkTable is { Count: > 0 })
        {
            traceId = profile.LinkTable[0].TraceId;
            spanId = profile.LinkTable[0].SpanId;
        }

        // Resolve sample type/unit
        var sampleType = Resolve(profile.SampleType?.TypeStrindex);
        var sampleUnit = Resolve(profile.SampleType?.UnitStrindex);

        // Header row
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

        // Functions — dereference all string table indices
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

        // Locations — dereference lines with function ordinals preserved for joins
        var locations = new List<ProfileLocationRow>();
        foreach (var (loc, i) in (profile.LocationTable ?? []).Select(static (l, i) => (l, i)))
        {
            string? linesJson = null;
            if (loc.Lines is { Count: > 0 })
            {
                linesJson = JsonSerializer.Serialize(
                    loc.Lines.Select(static line => new
                    {
                        functionOrdinal = line.FunctionIndex,
                        line = line.Line,
                        column = line.Column
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

        // Mappings — dereference filename
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

        // Samples — resolve link references, serialize values/timestamps as JSON
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
                TimestampsJson = s.TimestampsUnixNano is { Count: > 0 } ? JsonSerializer.Serialize(s.TimestampsUnixNano) : null
            });
        }

        // Stacks — serialize location ordinals as JSON array
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
    }

    #endregion

    #region Proto Log Conversion

    /// <summary>
    ///     Converts protobuf OTLP log request to storage rows.
    ///     Maps proto → JSON DTO → reuses existing <see cref="ConvertLogsToStorageRows" />.
    /// </summary>
    public static List<LogStorageRow> ConvertProtoLogsToStorageRows(ExportLogsServiceRequestProto proto)
    {
        var jsonDto = new OtlpExportLogsServiceRequest
        {
            ResourceLogs = proto.ResourceLogs.Select(static rl => new OtlpResourceLogs
            {
                Resource = rl.Resource is not null
                    ? new OtlpResource { Attributes = rl.Resource.Attributes.Select(MapProtoKeyValue).ToList() }
                    : null,
                ScopeLogs = rl.ScopeLogs.Select(static sl => new OtlpScopeLogs
                {
                    LogRecords = sl.LogRecords.Select(static lr => new OtlpLogRecord
                    {
                        TimeUnixNano = lr.TimeUnixNano,
                        ObservedTimeUnixNano = lr.ObservedTimeUnixNano,
                        SeverityNumber = lr.SeverityNumber,
                        SeverityText = lr.SeverityText,
                        Body = lr.Body is not null ? MapProtoAnyValue(lr.Body) : null,
                        Attributes = lr.Attributes?.Select(MapProtoKeyValue).ToList(),
                        TraceId = lr.TraceId,
                        SpanId = lr.SpanId
                    }).ToList()
                }).ToList()
            }).ToList()
        };
        return ConvertLogsToStorageRows(jsonDto);
    }

    private static OtlpKeyValue MapProtoKeyValue(OtlpKeyValueProto proto) =>
        new() { Key = proto.Key, Value = proto.Value is not null ? MapProtoAnyValue(proto.Value) : null };

    private static OtlpAnyValue MapProtoAnyValue(OtlpAnyValueProto proto) =>
        new()
        {
            StringValue = proto.StringValue,
            BoolValue = proto.BoolValue,
            IntValue = proto.IntValue,
            DoubleValue = proto.DoubleValue,
            ArrayValue = proto.ArrayValue is { Count: > 0 }
                ? new OtlpArrayValue { Values = proto.ArrayValue.Select(MapProtoAnyValue).ToList() }
                : null,
            KvlistValue = proto.KvlistValue is { Count: > 0 }
                ? new OtlpKeyValueList { Values = proto.KvlistValue.Select(MapProtoKeyValue).ToList() }
                : null,
            BytesValue = proto.BytesValue is not null ? Convert.ToBase64String(proto.BytesValue) : null
        };

    #endregion
}
