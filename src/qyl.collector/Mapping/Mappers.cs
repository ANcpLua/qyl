// =============================================================================
// qyl Model Mappers
// 
// Transforms internal storage models (SpanRecord, SessionSummary) into API DTOs.
// This is the bridge between DuckDB storage and the OpenAPI contract.
// =============================================================================

using System.Text.Json;
using qyl.collector.Query;
using qyl.collector.Storage;
using qyl.collector.Contracts;

namespace qyl.collector.Mapping;

/// <summary>
/// Maps SpanRecord (storage) → SpanDto (API)
/// </summary>
public static class SpanMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] SpanKindNames =
        ["unspecified", "internal", "server", "client", "producer", "consumer"];

    /// <summary>
    /// Maps a SpanRecord from DuckDB storage to API DTO
    /// </summary>
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
            Links = [], // TODO: Add links support to SpanRecord
            GenAI = ExtractGenAIData(record)
        };
    }

    /// <summary>
    /// Maps multiple SpanRecords with a service lookup
    /// </summary>
    public static List<SpanDto> ToDtos(
        IEnumerable<SpanRecord> records,
        Func<SpanRecord, (string ServiceName, string? ServiceVersion)> serviceResolver)
    {
        return records.Select(r =>
        {
            var (serviceName, serviceVersion) = serviceResolver(r);
            return ToDto(r, serviceName, serviceVersion);
        }).ToList();
    }

    private static string MapSpanKind(string? kind)
    {
        // SpanRecord.Kind might be int as string or name
        if (int.TryParse(kind, out var kindInt) && kindInt >= 0 && kindInt < SpanKindNames.Length)
            return SpanKindNames[kindInt];

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
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions) ?? [];
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
            var events = JsonSerializer.Deserialize<List<RawSpanEvent>>(json, JsonOptions);
            return events?.Select(e => new SpanEventDto
            {
                Name = e.Name ?? "unknown",
                Timestamp = e.Timestamp?.ToString("O") ?? DateTime.UtcNow.ToString("O"),
                Attributes = e.Attributes
            }).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static GenAISpanDataDto? ExtractGenAIData(SpanRecord record)
    {
        // Check if this is a GenAI span (has tokens or provider)
        if (record.TokensIn is null && record.TokensOut is null && string.IsNullOrEmpty(record.ProviderName))
            return null;

        return new GenAISpanDataDto
        {
            ProviderName = record.ProviderName,
            OperationName = ExtractOperationName(record.Name),
            RequestModel = record.RequestModel,
            ResponseModel = null, // TODO: Add to SpanRecord if needed
            InputTokens = record.TokensIn,
            OutputTokens = record.TokensOut,
            TotalTokens = (record.TokensIn ?? 0) + (record.TokensOut ?? 0),
            CostUsd = record.CostUsd.HasValue ? (double)record.CostUsd.Value : null,
            Temperature = null, // Extract from attributes if needed
            MaxTokens = null,
            FinishReason = null,
            ToolName = ExtractToolName(record),
            ToolCallId = null
        };
    }

    private static string? ExtractOperationName(string spanName)
    {
        // Parse "chat openai.chat" → "chat"
        var parts = spanName.Split(' ', 2);
        return parts.Length > 0 ? parts[0] : null;
    }

    private static string? ExtractToolName(SpanRecord record)
    {
        // Check attributes for tool name
        var attrs = ParseAttributes(record.Attributes);
        if (attrs.TryGetValue("gen_ai.tool.name", out var toolName))
            return toolName?.ToString();
        return null;
    }

    // Used for JSON deserialization of span events stored in DuckDB
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by JsonSerializer.Deserialize")]
    private sealed class RawSpanEvent
    {
        public string? Name { get; init; }
        public DateTime? Timestamp { get; init; }
        public Dictionary<string, object?>? Attributes { get; init; }
    }
}

/// <summary>
/// Maps SessionSummary (aggregation) → SessionDto (API)
/// </summary>
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
            Services = summary.Services.ToList(),
            TraceIds = [], // TODO: Populate if needed
            IsActive = (DateTime.UtcNow - lastActivity).TotalMinutes < 5,
            GenAIStats = new SessionGenAIStatsDto
            {
                TotalInputTokens = summary.InputTokens,
                TotalOutputTokens = summary.OutputTokens,
                TotalTokens = summary.TotalTokens,
                TotalCostUsd = (double)summary.TotalCostUsd,
                RequestCount = summary.GenAiRequestCount,
                ToolCallCount = 0, // TODO: Add to SessionSummary
                Models = summary.Models.ToList(),
                Providers = ExtractProviders(summary),
                PrimaryModel = summary.Models.Count > 0 ? summary.Models[0] : null
            },
            Attributes = null
        };
    }

    public static List<SessionDto> ToDtos(IEnumerable<SessionSummary> summaries)
    {
        return summaries.Select(ToDto).ToList();
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
        // Extract unique providers from models (e.g., "gpt-4o" → "openai")
        // This is a heuristic - ideally track providers explicitly
        var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in summary.Models)
        {
            var provider = InferProvider(model);
            if (provider is not null)
                providers.Add(provider);
        }

        return providers.ToList();
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
