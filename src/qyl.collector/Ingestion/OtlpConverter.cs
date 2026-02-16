// =============================================================================
// qyl OTLP Conversion - Single Source of Truth for OTLP → SpanStorageRow
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.39.0
// =============================================================================

using qyl.collector.Grpc;

namespace qyl.collector.Ingestion;

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
                        CreateStorageRowFromProto(span, serviceName, attributes, baggageJson, effectiveSchemaUrl);
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
        string? schemaUrl) =>
        CreateStorageRow(
            span.SpanId, span.TraceId, span.ParentSpanId, span.Name,
            span.Kind, span.StartTimeUnixNano, span.EndTimeUnixNano,
            span.Status?.Code, span.Status?.Message,
            serviceName, attributes, baggageJson, schemaUrl);

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
                    spans.Add(CreateStorageRowFromJson(span, serviceName, attributes, baggageJson, effectiveSchemaUrl));
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
        string? schemaUrl) =>
        CreateStorageRow(
            span.SpanId, span.TraceId, span.ParentSpanId, span.Name,
            span.Kind, span.StartTimeUnixNano, span.EndTimeUnixNano,
            span.Status?.Code, span.Status?.Message,
            serviceName, attributes, baggageJson, schemaUrl);

    #endregion

    #region Shared Helpers

    private static SpanStorageRow CreateStorageRow(
        string? spanId, string? traceId, string? parentSpanId, string? name,
        int? kind, ulong startNano, ulong endNano,
        int? statusCode, string? statusMessage,
        string serviceName, Dictionary<string, string> attributes,
        string? baggageJson, string? schemaUrl)
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
            ResourceJson = null,
            BaggageJson = baggageJson,
            SchemaUrl = schemaUrl
        };
    }

    private static string? ConvertJsonValueToString(OtlpAnyValue? value) =>
        value?.StringValue
        ?? value?.IntValue?.ToString()
        ?? value?.DoubleValue?.ToString(CultureInfo.InvariantCulture)
        ?? value?.BoolValue?.ToString().ToLowerInvariant();

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
#pragma warning disable QYL0002 // Fallback to deprecated attributes for backward compatibility
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
#pragma warning restore QYL0002

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

    private static long? ParseNullableLong(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.TryParseInt64();
    }

    private static double? ParseNullableDouble(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

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
}
