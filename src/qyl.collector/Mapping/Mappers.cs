namespace qyl.collector.Mapping;

public static class SpanMapper
{
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
        var startTime = UnixNanoToDateTime(record.StartTimeUnixNano);
        var endTime = UnixNanoToDateTime(record.EndTimeUnixNano);
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
            Events = [], // Events stored in AttributesJson if present
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
        .. records.Select(r =>
        {
            var (serviceName, serviceVersion) = serviceResolver(r);
            return ToDto(r, serviceName, serviceVersion);
        })
    ];

    /// <summary>
    ///     Converts UnixNano (ulong nanoseconds since epoch) to DateTime.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DateTime UnixNanoToDateTime(ulong unixNano)
    {
        // 1 tick = 100 nanoseconds
        var ticks = (long)(unixNano / 100);
        return DateTime.UnixEpoch.AddTicks(ticks);
    }

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

    [RequiresUnreferencedCode("Deserializes dynamic OTLP attribute values")]
    [RequiresDynamicCode("Deserializes dynamic OTLP attribute values")]
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
            MaxTokens = null,
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
            Services = [], // Services are now inferred from spans, not tracked on session
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
