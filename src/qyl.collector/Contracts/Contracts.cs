using System.Text.Json.Serialization;

namespace qyl.collector.Contracts;

public sealed record SpanDto
{
    public required string TraceId { get; init; }

    public required string SpanId { get; init; }

    public string? ParentSpanId { get; init; }

    public string? SessionId { get; init; }

    public required string Name { get; init; }

    public required string Kind { get; init; }

    public required string Status { get; init; }

    public string? StatusMessage { get; init; }

    public required string StartTime { get; init; }

    public required string EndTime { get; init; }

    public required double DurationMs { get; init; }

    public required string ServiceName { get; init; }

    public string? ServiceVersion { get; init; }

    public Dictionary<string, object?> Attributes { get; init; } = [];

    public List<SpanEventDto> Events { get; init; } = [];

    public List<SpanLinkDto> Links { get; init; } = [];

    [JsonPropertyName("genai")]
    public GenAiSpanDataDto? GenAi { get; init; }
}

public sealed record SpanEventDto
{
    public required string Name { get; init; }
    public required string Timestamp { get; init; }
    public Dictionary<string, object?>? Attributes { get; init; }
}

public sealed record SpanLinkDto
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public Dictionary<string, object?>? Attributes { get; init; }
}

public sealed record GenAiSpanDataDto
{
    public string? ProviderName { get; init; }

    public string? OperationName { get; init; }

    public string? RequestModel { get; init; }

    public string? ResponseModel { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public int? TotalTokens { get; init; }

    public double? CostUsd { get; init; }

    public double? Temperature { get; init; }

    public int? MaxTokens { get; init; }

    public string? FinishReason { get; init; }

    public string? ToolName { get; init; }

    public string? ToolCallId { get; init; }
}

public sealed record SessionDto
{
    public required string SessionId { get; init; }
    public required string StartTime { get; init; }
    public required string LastActivity { get; init; }
    public required double DurationMs { get; init; }
    public required int SpanCount { get; init; }
    public required int TraceCount { get; init; }
    public required int ErrorCount { get; init; }
    public required double ErrorRate { get; init; }

    public required List<string> Services { get; init; }

    public required List<string> TraceIds { get; init; }

    public bool IsActive { get; init; }

    [JsonPropertyName("genaiStats")]
    public required SessionGenAiStatsDto GenAiStats { get; init; }

    public Dictionary<string, object?>? Attributes { get; init; }
}

public sealed record SessionGenAiStatsDto
{
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
    public int TotalTokens { get; init; }
    public double TotalCostUsd { get; init; }

    public int RequestCount { get; init; }

    public int ToolCallCount { get; init; }

    public List<string> Models { get; init; } = [];

    public List<string> Providers { get; init; } = [];

    public string? PrimaryModel { get; init; }
}

public sealed record SessionListResponseDto
{
    public required List<SessionDto> Sessions { get; init; }
    public required int Total { get; init; }
    public required bool HasMore { get; init; }
}

public sealed record SpanListResponseDto
{
    public required List<SpanDto> Spans { get; init; }
}

public sealed record TraceResponseDto
{
    public string? TraceId { get; init; }
    public required List<SpanDto> Spans { get; init; }
    public SpanDto? RootSpan { get; init; }
    public double? DurationMs { get; init; }
    public string? Status { get; init; }
}

public sealed record TelemetryEventDto
{
    public required string EventType { get; init; }
    public object? Data { get; init; }
    public required string Timestamp { get; init; }
}

public sealed record SpanBatchDto
{
    public required List<SpanDto> Spans { get; init; }
}
