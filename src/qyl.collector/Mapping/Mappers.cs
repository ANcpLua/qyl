using qyl.collector.Core;

namespace qyl.collector.Mapping;

public static class SpanMapper
{
    public static SpanRecord ToRecord(SpanStorageRow row)
    {
        return new SpanRecord
        {
            SpanId = new SpanId(row.SpanId),
            TraceId = new TraceId(row.TraceId),
            ParentSpanId = row.ParentSpanId is { } parentId ? new SpanId(parentId) : default(SpanId?),
            SessionId = row.SessionId is { } sessId ? new SessionId(sessId) : default(SessionId?),
            Name = row.Name,
            Kind = (Qyl.Enums.SpanKind)row.Kind,
            StartTimeUnixNano = (long)row.StartTimeUnixNano,
            EndTimeUnixNano = (long)row.EndTimeUnixNano,
            DurationNs = (long)row.DurationNs,
            StatusCode = (Qyl.Enums.StatusCode)row.StatusCode,
            StatusMessage = row.StatusMessage,
            ServiceName = row.ServiceName,
            GenAiSystem = row.GenAiSystem,
            GenAiRequestModel = row.GenAiRequestModel,
            GenAiResponseModel = row.GenAiResponseModel,
            GenAiInputTokens = row.GenAiInputTokens,
            GenAiOutputTokens = row.GenAiOutputTokens,
            GenAiTemperature = row.GenAiTemperature,
            GenAiStopReason = row.GenAiStopReason,
            GenAiToolName = row.GenAiToolName,
            GenAiToolCallId = row.GenAiToolCallId,
            GenAiCostUsd = row.GenAiCostUsd,
            AttributesJson = row.AttributesJson,
            ResourceJson = row.ResourceJson,
            CreatedAt = row.CreatedAt ?? DateTimeOffset.UtcNow
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] SpanKindNames =
        ["unspecified", "internal", "server", "client", "producer", "consumer"];

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    public static SpanDto ToDto(SpanStorageRow record, string serviceName, string? serviceVersion = null)
    {
        // Convert UnixNano (ulong) to DateTime
        var startTime = TimeConversions.UnixNanoToDateTime(record.StartTimeUnixNano);
        var endTime = TimeConversions.UnixNanoToDateTime(record.EndTimeUnixNano);
        var durationMs = record.DurationNs / 1_000_000.0; // ns → ms

        return new SpanDto
        {
            TraceId = record.TraceId,
            SpanId = record.SpanId,
            ParentSpanId = record.ParentSpanId,
            SessionId = record.SessionId,
            Name = record.Name,
            Kind = MapSpanKind(record.Kind),
            Status = MapStatus(record.StatusCode),
            StatusMessage = record.StatusMessage,
            StartTime = startTime.ToString("O"),
            EndTime = endTime.ToString("O"),
            DurationMs = durationMs,
            ServiceName = serviceName,
            ServiceVersion = serviceVersion,
            Attributes = ParseAttributes(record.AttributesJson),
            Events = [],
            Links = [],
            GenAi = ExtractGenAiData(record)
        };
    }

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    public static SpanDto ToDto(SpanRecord record, string serviceName, string? serviceVersion = null)
    {
        // Convert protocol UnixNano (long) to DateTime
        var startTime = TimeConversions.UnixNanoToDateTime((ulong)record.StartTimeUnixNano.Value);
        var endTime = TimeConversions.UnixNanoToDateTime((ulong)record.EndTimeUnixNano.Value);
        var durationMs = record.DurationNs.Value / 1_000_000.0;

        return new SpanDto
        {
            TraceId = record.TraceId.Value,
            SpanId = record.SpanId.Value,
            ParentSpanId = record.ParentSpanId?.Value,
            SessionId = record.SessionId?.Value,
            Name = record.Name,
            Kind = record.Kind.ToString().ToLowerInvariant(),
            Status = record.StatusCode.ToString().ToLowerInvariant(),
            StatusMessage = record.StatusMessage,
            StartTime = startTime.ToString("O"),
            EndTime = endTime.ToString("O"),
            DurationMs = durationMs,
            ServiceName = serviceName,
            ServiceVersion = serviceVersion,
            Attributes = ParseAttributes(record.AttributesJson),
            Events = [],
            Links = [],
            GenAi = ExtractGenAiData(record)
        };
    }

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    public static List<SpanDto> ToDtos(
        IEnumerable<SpanStorageRow> records,
        Func<SpanStorageRow, (string ServiceName, string? ServiceVersion)> serviceResolver) =>
    [
        .. records.Where(static _ => true).Select(r =>
        {
            var (serviceName, serviceVersion) = serviceResolver(r);
            return ToDto(r, serviceName, serviceVersion);
        })
    ];

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    public static List<SpanDto> ToDtos(
        IEnumerable<SpanRecord> records,
        Func<SpanRecord, (string ServiceName, string? ServiceVersion)> serviceResolver) =>
    [
        .. records.Where(static _ => true).Select(r =>
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

    private static GenAiSpanDataDto? ExtractGenAiData(SpanStorageRow record)
    {
        if (record.GenAiInputTokens is null && record.GenAiOutputTokens is null &&
            string.IsNullOrEmpty(record.GenAiSystem))
            return null;

        return new GenAiSpanDataDto
        {
            ProviderName = record.GenAiSystem,
            OperationName = ExtractOperationName(record.Name),
            RequestModel = record.GenAiRequestModel,
            ResponseModel = record.GenAiResponseModel,
            InputTokens = record.GenAiInputTokens,
            OutputTokens = record.GenAiOutputTokens,
            TotalTokens = (record.GenAiInputTokens ?? 0) + (record.GenAiOutputTokens ?? 0),
            CostUsd = record.GenAiCostUsd,
            Temperature = record.GenAiTemperature,
            FinishReason = record.GenAiStopReason,
            ToolName = record.GenAiToolName,
            ToolCallId = record.GenAiToolCallId
        };
    }

    private static GenAiSpanDataDto? ExtractGenAiData(SpanRecord record)
    {
        if (record.GenAiInputTokens is null && record.GenAiOutputTokens is null &&
            string.IsNullOrEmpty(record.GenAiSystem))
            return null;

        return new GenAiSpanDataDto
        {
            ProviderName = record.GenAiSystem,
            OperationName = ExtractOperationName(record.Name),
            RequestModel = record.GenAiRequestModel,
            ResponseModel = record.GenAiResponseModel,
            InputTokens = record.GenAiInputTokens?.Value,
            OutputTokens = record.GenAiOutputTokens?.Value,
            TotalTokens = (record.GenAiInputTokens?.Value ?? 0) + (record.GenAiOutputTokens?.Value ?? 0),
            CostUsd = record.GenAiCostUsd?.Value,
            Temperature = record.GenAiTemperature?.Value,
            FinishReason = record.GenAiStopReason,
            ToolName = record.GenAiToolName,
            ToolCallId = record.GenAiToolCallId
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
            Services = [],
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
        new()
        {
            Sessions = ToDtos(summaries), Total = total, HasMore = hasMore
        };

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
            _ when model.StartsWith("gpt", StringComparison.OrdinalIgnoreCase) => "openai",
            _ when model.StartsWith("o1", StringComparison.OrdinalIgnoreCase) => "openai",
            _ when model.StartsWith("claude", StringComparison.OrdinalIgnoreCase) => "anthropic",
            _ when model.StartsWith("gemini", StringComparison.OrdinalIgnoreCase) => "google",
            _ when model.StartsWith("llama", StringComparison.OrdinalIgnoreCase) => "meta",
            _ when model.StartsWith("mistral", StringComparison.OrdinalIgnoreCase) => "mistral",
            _ when model.StartsWith("command", StringComparison.OrdinalIgnoreCase) => "cohere",
            _ => null
        };
}
