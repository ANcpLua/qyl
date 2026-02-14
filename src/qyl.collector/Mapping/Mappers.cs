using qyl.collector.Core;
using SpanKind = Qyl.OTel.Enums.SpanKind;

namespace qyl.collector.Mapping;

public static class SpanMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] SpanKindNames =
        ["unspecified", "internal", "server", "client", "producer", "consumer"];

    public static SpanRecord ToRecord(SpanStorageRow row) =>
        new()
        {
            SpanId = new SpanId(row.SpanId),
            TraceId = new TraceId(row.TraceId),
            ParentSpanId = row.ParentSpanId is { } parentId ? new SpanId(parentId) : default(SpanId?),
            SessionId = row.SessionId is { } sessId ? new SessionId(sessId) : default(SessionId?),
            Name = row.Name,
            Kind = (SpanKind)row.Kind,
            StartTimeUnixNano = (long)row.StartTimeUnixNano,
            EndTimeUnixNano = (long)row.EndTimeUnixNano,
            DurationNs = (long)row.DurationNs,
            StatusCode = (SpanStatusCode)row.StatusCode,
            StatusMessage = row.StatusMessage,
            ServiceName = row.ServiceName,
            GenAiProviderName = row.GenAiProviderName,
            GenAiRequestModel = row.GenAiRequestModel,
            GenAiResponseModel = row.GenAiResponseModel,
            GenAiInputTokens = (int?)row.GenAiInputTokens,
            GenAiOutputTokens = (int?)row.GenAiOutputTokens,
            GenAiTemperature = row.GenAiTemperature,
            GenAiStopReason = row.GenAiStopReason,
            GenAiToolName = row.GenAiToolName,
            GenAiToolCallId = row.GenAiToolCallId,
            GenAiCostUsd = row.GenAiCostUsd,
            AttributesJson = row.AttributesJson,
            ResourceJson = row.ResourceJson,
            BaggageJson = row.BaggageJson,
            SchemaUrl = row.SchemaUrl,
            CreatedAt = row.CreatedAt ?? TimeProvider.System.GetUtcNow()
        };

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    public static SpanDto ToDto(SpanStorageRow record, string serviceName, string? serviceVersion = null) =>
        ToDtoCore(
            record.TraceId, record.SpanId, record.ParentSpanId, record.SessionId,
            record.Name, MapSpanKind(record.Kind), MapStatus(record.StatusCode), record.StatusMessage,
            record.StartTimeUnixNano, record.EndTimeUnixNano, record.DurationNs,
            serviceName, serviceVersion,
            record.AttributesJson, record.BaggageJson, record.SchemaUrl,
            record.GenAiInputTokens, record.GenAiOutputTokens, record.GenAiProviderName,
            record.GenAiRequestModel, record.GenAiResponseModel, record.GenAiCostUsd,
            record.GenAiTemperature, record.GenAiStopReason, record.GenAiToolName, record.GenAiToolCallId);

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    public static List<SpanDto> ToDtos(
        IEnumerable<SpanStorageRow> records,
        Func<SpanStorageRow, (string ServiceName, string? ServiceVersion)> serviceResolver) =>
    [
        .. records.Select(r =>
        {
            var (serviceName, serviceVersion) = serviceResolver(r);
            return ToDto(r, serviceName, serviceVersion);
        })
    ];

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    public static SpanDto ToDto(SpanRecord record, string serviceName, string? serviceVersion = null) =>
        ToDtoCore(
            record.TraceId.Value, record.SpanId.Value, record.ParentSpanId?.Value, record.SessionId?.Value,
            record.Name, record.Kind.ToString().ToLowerInvariant(), record.StatusCode.ToString().ToLowerInvariant(),
            record.StatusMessage,
            (ulong)record.StartTimeUnixNano, (ulong)record.EndTimeUnixNano, (ulong)record.DurationNs,
            serviceName, serviceVersion,
            record.AttributesJson, record.BaggageJson, record.SchemaUrl,
            record.GenAiInputTokens, record.GenAiOutputTokens, record.GenAiProviderName,
            record.GenAiRequestModel, record.GenAiResponseModel, record.GenAiCostUsd,
            record.GenAiTemperature, record.GenAiStopReason, record.GenAiToolName, record.GenAiToolCallId);

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    public static List<SpanDto> ToDtos(
        IEnumerable<SpanRecord> records,
        Func<SpanRecord, (string ServiceName, string? ServiceVersion)> serviceResolver) =>
    [
        .. records.Select(r =>
        {
            var (serviceName, serviceVersion) = serviceResolver(r);
            return ToDto(r, serviceName, serviceVersion);
        })
    ];

    private static string MapSpanKind(byte kind) =>
        kind < SpanKindNames.Length ? SpanKindNames[kind] : "unspecified";

    private static string MapStatus(byte statusCode) =>
        statusCode switch
        {
            0 => "unset",
            1 => "ok",
            2 => "error",
            _ => "unset"
        };

    [RequiresUnreferencedCode("Deserializes dynamic OTLP attribute values")]
    [RequiresDynamicCode("Deserializes dynamic OTLP attribute values")]
    private static Dictionary<string, object?> ParseAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    [RequiresUnreferencedCode("Deserializes W3C baggage JSON")]
    [RequiresDynamicCode("Deserializes W3C baggage JSON")]
    private static Dictionary<string, string>? ParseBaggage(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    private static SpanDto ToDtoCore(
        string traceId, string spanId, string? parentSpanId, string? sessionId,
        string name, string kind, string status, string? statusMessage,
        ulong startTimeUnixNano, ulong endTimeUnixNano, ulong durationNs,
        string serviceName, string? serviceVersion,
        string? attributesJson, string? baggageJson, string? schemaUrl,
        long? genAiInputTokens, long? genAiOutputTokens, string? genAiProviderName,
        string? genAiRequestModel, string? genAiResponseModel, double? genAiCostUsd,
        double? genAiTemperature, string? genAiStopReason, string? genAiToolName, string? genAiToolCallId)
    {
        var startTime = TimeConversions.UnixNanoToDateTime(startTimeUnixNano);
        var endTime = TimeConversions.UnixNanoToDateTime(endTimeUnixNano);

        return new SpanDto
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            SessionId = sessionId,
            Name = name,
            Kind = kind,
            Status = status,
            StatusMessage = statusMessage,
            StartTime = startTime.ToString("O"),
            EndTime = endTime.ToString("O"),
            DurationMs = durationNs / 1_000_000.0,
            ServiceName = serviceName,
            ServiceVersion = serviceVersion,
            Attributes = ParseAttributes(attributesJson),
            Events = [],
            Links = [],
            GenAi = ExtractGenAiData(
                genAiInputTokens, genAiOutputTokens, genAiProviderName, name,
                genAiRequestModel, genAiResponseModel, genAiCostUsd,
                genAiTemperature, genAiStopReason, genAiToolName, genAiToolCallId),
            Baggage = ParseBaggage(baggageJson),
            SchemaUrl = schemaUrl
        };
    }

    private static GenAiSpanDataDto? ExtractGenAiData(
        long? inputTokens, long? outputTokens, string? providerName, string spanName,
        string? requestModel, string? responseModel, double? costUsd,
        double? temperature, string? stopReason, string? toolName, string? toolCallId)
    {
        if (inputTokens is null && outputTokens is null && string.IsNullOrEmpty(providerName))
            return null;

        return new GenAiSpanDataDto
        {
            ProviderName = providerName,
            OperationName = ExtractOperationName(spanName),
            RequestModel = requestModel,
            ResponseModel = responseModel,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = (inputTokens ?? 0) + (outputTokens ?? 0),
            CostUsd = costUsd,
            Temperature = temperature,
            FinishReason = stopReason,
            ToolName = toolName,
            ToolCallId = toolCallId
        };
    }

    private static string? ExtractOperationName(string spanName)
    {
        var parts = spanName.Split(' ', 2);
        return parts.Length > 0 ? parts[0] : null;
    }
}

public static class SessionMapper
{
    public static SessionDto ToDto(SessionQueryRow summary)
    {
        var startTime = summary.StartTime.ToUniversalTime();
        var lastActivity = summary.LastActivity.ToUniversalTime();

        return new SessionDto
        {
            SessionId = summary.SessionId,
            StartTime = startTime.ToString("O"),
            LastActivity = lastActivity.ToString("O"),
            DurationMs = summary.DurationMs,
            SpanCount = summary.SpanCount,
            TraceCount = summary.TraceCount,
            ErrorCount = summary.ErrorCount,
            ErrorRate = summary.ErrorRate,
            Services = [.. summary.Services],
            TraceIds = [],
            IsActive = (TimeProvider.System.GetUtcNow() - lastActivity).TotalMinutes < 5,
            GenAiStats = new SessionGenAiStatsDto
            {
                TotalInputTokens = summary.InputTokens,
                TotalOutputTokens = summary.OutputTokens,
                TotalTokens = summary.TotalTokens,
                TotalCostUsd = summary.TotalCostUsd,
                RequestCount = summary.GenAiRequestCount,
                ToolCallCount = 0,
                Models = [.. summary.Models],
                Providers = ExtractProviders(summary),
                PrimaryModel = summary.Models.Count > 0 ? summary.Models[0] : null
            },
            Attributes = null
        };
    }

    public static List<SessionDto> ToDtos(IEnumerable<SessionQueryRow> summaries) => [.. summaries.Select(ToDto)];

    public static SessionListResponseDto ToListResponse(
        IEnumerable<SessionQueryRow> summaries,
        int total,
        bool hasMore) =>
        new() { Sessions = ToDtos(summaries), Total = total, HasMore = hasMore };

    private static List<string> ExtractProviders(SessionQueryRow summary)
    {
        var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in summary.Models)
        {
            var provider = InferProvider(model);
            if (provider is not null)
                providers.Add(provider);
        }

        return [.. providers];
    }

    private static string? InferProvider(string model) =>
        model switch
        {
            _ when model.StartsWithIgnoreCase("gpt") => "openai",
            _ when model.StartsWithIgnoreCase("o1") => "openai",
            _ when model.StartsWithIgnoreCase("claude") => "anthropic",
            _ when model.StartsWithIgnoreCase("gemini") => "google",
            _ when model.StartsWithIgnoreCase("llama") => "meta",
            _ when model.StartsWithIgnoreCase("mistral") => "mistral",
            _ when model.StartsWithIgnoreCase("command") => "cohere",
            _ => null
        };
}
