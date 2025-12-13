using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using qyl.collector.Contracts;
using qyl.collector.Query;
using qyl.collector.Storage;

namespace qyl.collector.Mapping;

public static class SpanMapper
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] _spanKindNames =
        ["unspecified", "internal", "server", "client", "producer", "consumer"];

    public static SpanDto ToDto(SpanRecord record, string serviceName, string? serviceVersion = null)
    {
        var startTime = record.StartTime.ToUniversalTime();
        var endTime = record.EndTime.ToUniversalTime();

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
            DurationMs = (endTime - startTime).TotalMilliseconds,
            ServiceName = serviceName,
            ServiceVersion = serviceVersion,
            Attributes = ParseAttributes(record.Attributes),
            Events = ParseEvents(record.Events),
            Links = [],
            GenAi = ExtractGenAiData(record)
        };
    }

    public static List<SpanDto> ToDtos(
        IEnumerable<SpanRecord> records,
        Func<SpanRecord, (string ServiceName, string? ServiceVersion)> serviceResolver)
    {
        return
        [
            .. records.Select(r =>
            {
                var (serviceName, serviceVersion) = serviceResolver(r);
                return ToDto(r, serviceName, serviceVersion);
            })
        ];
    }

    private static string MapSpanKind(string? kind)
    {
        if (int.TryParse(kind, out var kindInt) && kindInt >= 0 && kindInt < _spanKindNames.Length)
            return _spanKindNames[kindInt];

        return kind?.ToLowerInvariant() switch
        {
            "internal" => "internal",
            "server" => "server",
            "client" => "client",
            "producer" => "producer",
            "consumer" => "consumer",
            _ => "unspecified"
        };
    }

    private static string MapStatus(int? statusCode)
    {
        return statusCode switch
        {
            0 => "unset",
            1 => "ok",
            2 => "error",
            _ => "unset"
        };
    }

    private static Dictionary<string, object?> ParseAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, _jsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<SpanEventDto> ParseEvents(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var events = JsonSerializer.Deserialize<List<RawSpanEvent>>(json, _jsonOptions);
            return events?.Select(e => new SpanEventDto
            {
                Name = e.Name ?? "unknown",
                Timestamp = e.Timestamp?.ToString("O") ?? TimeProvider.System.GetUtcNow().ToString("O"),
                Attributes = e.Attributes
            }).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static GenAiSpanDataDto? ExtractGenAiData(SpanRecord record)
    {
        if (record.TokensIn is null && record.TokensOut is null && string.IsNullOrEmpty(record.ProviderName))
            return null;

        return new GenAiSpanDataDto
        {
            ProviderName = record.ProviderName,
            OperationName = ExtractOperationName(record.Name),
            RequestModel = record.RequestModel,
            ResponseModel = null,
            InputTokens = record.TokensIn,
            OutputTokens = record.TokensOut,
            TotalTokens = (record.TokensIn ?? 0) + (record.TokensOut ?? 0),
            CostUsd = record.CostUsd.HasValue ? (double)record.CostUsd.Value : null,
            Temperature = null,
            MaxTokens = null,
            FinishReason = null,
            ToolName = ExtractToolName(record),
            ToolCallId = null
        };
    }

    private static string? ExtractOperationName(string spanName)
    {
        var parts = spanName.Split(' ', 2);
        return parts.Length > 0 ? parts[0] : null;
    }

    private static string? ExtractToolName(SpanRecord record)
    {
        var attrs = ParseAttributes(record.Attributes);
        if (attrs.TryGetValue("gen_ai.tool.name", out var toolName))
            return toolName?.ToString();
        return null;
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated by JsonSerializer.Deserialize")]
    private sealed class RawSpanEvent
    {
        public string? Name { get; init; }
        public DateTime? Timestamp { get; init; }
        public Dictionary<string, object?>? Attributes { get; init; }
    }
}

public static class SessionMapper
{
    public static SessionDto ToDto(SessionSummary summary)
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
                TotalCostUsd = (double)summary.TotalCostUsd,
                RequestCount = summary.GenAiRequestCount,
                ToolCallCount = 0,
                Models = [.. summary.Models],
                Providers = ExtractProviders(summary),
                PrimaryModel = summary.Models.Count > 0 ? summary.Models[0] : null
            },
            Attributes = null
        };
    }

    public static List<SessionDto> ToDtos(IEnumerable<SessionSummary> summaries)
    {
        return [.. summaries.Select(ToDto)];
    }

    public static SessionListResponseDto ToListResponse(
        IEnumerable<SessionSummary> summaries,
        int total,
        bool hasMore)
    {
        return new SessionListResponseDto
        {
            Sessions = ToDtos(summaries),
            Total = total,
            HasMore = hasMore
        };
    }

    private static List<string> ExtractProviders(SessionSummary summary)
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

    private static string? InferProvider(string model)
    {
        return model switch
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
}
