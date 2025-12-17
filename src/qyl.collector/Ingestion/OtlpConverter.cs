// =============================================================================
// qyl OTLP Conversion - Single Source of Truth for OTLP → SpanStorageRow
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.38.0
// =============================================================================

using qyl.collector.Grpc;

namespace qyl.collector.Ingestion;

/// <summary>
///     Converts OTLP protobuf/JSON to SpanStorageRow.
///     Single source of truth for both gRPC (port 4317) and HTTP (POST /v1/traces) endpoints.
/// </summary>
public static class OtlpConverter
{
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

            foreach (var scopeSpan in resourceSpan.ScopeSpans)
            foreach (var span in scopeSpan.Spans)
            {
                var attributes = ExtractAttributesFromProto(span.Attributes, serviceName);
                var spanRecord = CreateStorageRowFromProto(span, serviceName, attributes);
                spans.Add(spanRecord);
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
        if (value.DoubleValue.HasValue) return value.DoubleValue.Value.ToString();
        if (value.BoolValue.HasValue) return value.BoolValue.Value.ToString().ToLowerInvariant();
        if (value.BytesValue is not null) return Convert.ToBase64String(value.BytesValue);
        if (value.ArrayValue is not null)
        {
            var items = value.ArrayValue.Select(ConvertAnyValueToString).Where(v => v is not null).ToArray()!;
            return JsonSerializer.Serialize(items, QylSerializerContext.Default.StringArray);
        }

        if (value.KvlistValue is null) return null;
        var dict = value.KvlistValue
            .Where(kv => ConvertAnyValueToString(kv.Value) is not null)
            .ToDictionary(kv => kv.Key ?? "", kv => ConvertAnyValueToString(kv.Value)!);
        return JsonSerializer.Serialize(dict, QylSerializerContext.Default.DictionaryStringString);
    }

    private static SpanStorageRow CreateStorageRowFromProto(
        OtlpSpanProto span,
        string serviceName,
        Dictionary<string, string> attributes)
    {
        // Convert nanoseconds to DateTime (ulong nanoseconds -> ticks, 1 tick = 100ns)
        var startTime = DateTime.UnixEpoch.AddTicks((long)(span.StartTimeUnixNano / 100));
        var endTime = DateTime.UnixEpoch.AddTicks((long)(span.EndTimeUnixNano / 100));

        // Extract gen_ai attributes with schema normalization (fallback to deprecated names)
        var (providerName, requestModel, tokensIn, tokensOut) = ExtractGenAiAttributes(attributes);

        return new SpanStorageRow
        {
            TraceId = span.TraceId ?? "",
            SpanId = span.SpanId ?? "",
            ParentSpanId = string.IsNullOrEmpty(span.ParentSpanId) ? null : span.ParentSpanId,
            SessionId = attributes.GetValueOrDefault("session.id"),
            ServiceName = serviceName,
            Name = span.Name ?? "unknown",
            Kind = ConvertSpanKind(span.Kind),
            StartTime = startTime,
            EndTime = endTime,
            StatusCode = span.Status?.Code,
            StatusMessage = string.IsNullOrEmpty(span.Status?.Message) ? null : span.Status.Message,
            ProviderName = providerName,
            RequestModel = requestModel,
            TokensIn = tokensIn,
            TokensOut = tokensOut,
            CostUsd = null,
            Attributes = JsonSerializer.Serialize(attributes, QylSerializerContext.Default.DictionaryStringString),
            Events = span.Events?.Count > 0 ? SerializeProtoEvents(span.Events) : null
        };
    }

    private static string SerializeProtoEvents(List<OtlpSpanEventProto> events)
    {
        var eventList = events.Select(e => new OtlpEventJson(
            e.Name,
            e.TimeUnixNano,
            e.Attributes?.ToDictionary(
                a => a.Key ?? "",
                a => ConvertAnyValueToString(a.Value))
        )).ToArray();

        return JsonSerializer.Serialize(eventList, QylSerializerContext.Default.OtlpEventJsonArray);
    }

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
                                  .FirstOrDefault(a => a.Key == "service.name")?.Value?.StringValue
                              ?? "unknown";

            foreach (var scopeSpan in resourceSpan.ScopeSpans ?? [])
            foreach (var span in scopeSpan.Spans ?? [])
            {
                var attributes = ExtractAttributesFromJson(span.Attributes, serviceName);
                spans.Add(CreateStorageRowFromJson(span, serviceName, attributes));
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

            var value = attr.Value?.StringValue
                        ?? attr.Value?.IntValue?.ToString()
                        ?? attr.Value?.DoubleValue?.ToString()
                        ?? attr.Value?.BoolValue?.ToString()?.ToLowerInvariant();

            if (value is not null) attributes[attr.Key] = value;
        }

        return attributes;
    }

    private static SpanStorageRow CreateStorageRowFromJson(
        OtlpSpan span,
        string serviceName,
        Dictionary<string, string> attributes)
    {
        var startTime = DateTime.UnixEpoch.AddTicks(span.StartTimeUnixNano / 100);
        var endTime = DateTime.UnixEpoch.AddTicks(span.EndTimeUnixNano / 100);

        // Extract gen_ai attributes with schema normalization (fallback to deprecated names)
        var (providerName, requestModel, tokensIn, tokensOut) = ExtractGenAiAttributes(attributes);

        return new SpanStorageRow
        {
            TraceId = span.TraceId ?? "",
            SpanId = span.SpanId ?? "",
            ParentSpanId = string.IsNullOrEmpty(span.ParentSpanId) ? null : span.ParentSpanId,
            SessionId = attributes.GetValueOrDefault("session.id"),
            ServiceName = serviceName,
            Name = span.Name ?? "unknown",
            Kind = ConvertSpanKind(span.Kind),
            StartTime = startTime,
            EndTime = endTime,
            StatusCode = span.Status?.Code,
            StatusMessage = span.Status?.Message,
            ProviderName = providerName,
            RequestModel = requestModel,
            TokensIn = tokensIn,
            TokensOut = tokensOut,
            CostUsd = null,
            Attributes = JsonSerializer.Serialize(attributes, QylSerializerContext.Default.DictionaryStringString),
            Events = null // JSON endpoint doesn't include events currently
        };
    }

    #endregion

    #region Shared Helpers

    /// <summary>
    ///     Extracts GenAI attributes with fallback to deprecated OTel 1.38 names.
    /// </summary>
#pragma warning disable QYL0002 // Fallback to deprecated attributes for backward compatibility
    private static (string? ProviderName, string? RequestModel, long? TokensIn, long? TokensOut)
        ExtractGenAiAttributes(Dictionary<string, string> attributes)
    {
        var providerName = attributes.GetValueOrDefault("gen_ai.provider.name")
                           ?? attributes.GetValueOrDefault("gen_ai.system");

        var requestModel = attributes.GetValueOrDefault("gen_ai.request.model");

        var tokensIn = ParseNullableLong(
            attributes.GetValueOrDefault("gen_ai.usage.input_tokens")
            ?? attributes.GetValueOrDefault("gen_ai.usage.prompt_tokens"));

        var tokensOut = ParseNullableLong(
            attributes.GetValueOrDefault("gen_ai.usage.output_tokens")
            ?? attributes.GetValueOrDefault("gen_ai.usage.completion_tokens"));

        return (providerName, requestModel, tokensIn, tokensOut);
    }
#pragma warning restore QYL0002

    private static string? ConvertSpanKind(int? kind) =>
        kind switch
        {
            1 => "INTERNAL",
            2 => "SERVER",
            3 => "CLIENT",
            4 => "PRODUCER",
            5 => "CONSUMER",
            _ => null
        };

    private static long? ParseNullableLong(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return long.TryParse(value, out var result) ? result : null;
    }

    #endregion
}
