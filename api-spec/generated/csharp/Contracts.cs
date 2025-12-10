// =============================================================================
// qyl API DTOs
// 
// These records match the OpenAPI spec exactly.
// The API layer transforms internal models (SpanRecord, SessionSummary) into these.
// 
// DO NOT modify field names/types without updating openapi.yaml first!
// =============================================================================

using System.Text.Json.Serialization;

namespace qyl.Contracts;

// =============================================================================
// SPAN - The core telemetry unit
// =============================================================================

/// <summary>
/// OpenTelemetry Span with GenAI extensions.
/// Returned by /api/v1/traces/{traceId} and /api/v1/sessions/{sessionId}/spans
/// </summary>
public sealed record SpanDto
{
    /// <summary>W3C trace ID (32 hex chars)</summary>
    public required string TraceId { get; init; }
    
    /// <summary>Span ID (16 hex chars)</summary>
    public required string SpanId { get; init; }
    
    /// <summary>Parent span ID, null for root spans</summary>
    public string? ParentSpanId { get; init; }
    
    /// <summary>qyl session ID for grouping related traces</summary>
    public string? SessionId { get; init; }
    
    /// <summary>Operation name</summary>
    public required string Name { get; init; }
    
    /// <summary>OpenTelemetry SpanKind: unspecified, internal, server, client, producer, consumer</summary>
    public required string Kind { get; init; }
    
    /// <summary>OpenTelemetry StatusCode: unset, ok, error</summary>
    public required string Status { get; init; }
    
    public string? StatusMessage { get; init; }
    
    /// <summary>ISO 8601 timestamp</summary>
    public required string StartTime { get; init; }
    
    /// <summary>ISO 8601 timestamp</summary>
    public required string EndTime { get; init; }
    
    /// <summary>Duration in milliseconds (computed)</summary>
    public required double DurationMs { get; init; }
    
    /// <summary>service.name resource attribute</summary>
    public required string ServiceName { get; init; }
    
    /// <summary>service.version resource attribute</summary>
    public string? ServiceVersion { get; init; }
    
    /// <summary>Span attributes as key-value pairs</summary>
    public Dictionary<string, object?> Attributes { get; init; } = [];
    
    public List<SpanEventDto> Events { get; init; } = [];
    
    public List<SpanLinkDto> Links { get; init; } = [];
    
    /// <summary>Extracted GenAI semantic convention data (null for non-GenAI spans)</summary>
    [JsonPropertyName("genai")]
    public GenAISpanDataDto? GenAI { get; init; }
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

// =============================================================================
// GENAI - OpenTelemetry Semantic Conventions v1.38
// =============================================================================

/// <summary>
/// Extracted gen_ai.* attributes per OpenTelemetry Semantic Conventions v1.38.
/// See: https://opentelemetry.io/docs/specs/semconv/gen-ai/
/// </summary>
public sealed record GenAISpanDataDto
{
    /// <summary>gen_ai.system (e.g., "openai", "anthropic")</summary>
    public string? ProviderName { get; init; }
    
    /// <summary>gen_ai.operation.name</summary>
    public string? OperationName { get; init; }
    
    /// <summary>gen_ai.request.model</summary>
    public string? RequestModel { get; init; }
    
    /// <summary>gen_ai.response.model</summary>
    public string? ResponseModel { get; init; }
    
    /// <summary>gen_ai.usage.input_tokens</summary>
    public int? InputTokens { get; init; }
    
    /// <summary>gen_ai.usage.output_tokens</summary>
    public int? OutputTokens { get; init; }
    
    /// <summary>Computed (inputTokens + outputTokens)</summary>
    public int? TotalTokens { get; init; }
    
    /// <summary>qyl.cost.usd - estimated cost</summary>
    public double? CostUsd { get; init; }
    
    /// <summary>gen_ai.request.temperature</summary>
    public double? Temperature { get; init; }
    
    /// <summary>gen_ai.request.max_tokens</summary>
    public int? MaxTokens { get; init; }
    
    /// <summary>gen_ai.response.finish_reason</summary>
    public string? FinishReason { get; init; }
    
    /// <summary>gen_ai.tool.name (for tool call spans)</summary>
    public string? ToolName { get; init; }
    
    /// <summary>gen_ai.tool.call.id</summary>
    public string? ToolCallId { get; init; }
}

// =============================================================================
// SESSION - Aggregated view of related traces
// =============================================================================

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
    
    /// <summary>All services involved in this session</summary>
    public required List<string> Services { get; init; }
    
    public required List<string> TraceIds { get; init; }
    
    /// <summary>Activity within last 5 minutes</summary>
    public bool IsActive { get; init; }
    
    [JsonPropertyName("genaiStats")]
    public required SessionGenAIStatsDto GenAIStats { get; init; }
    
    /// <summary>Session-level metadata</summary>
    public Dictionary<string, object?>? Attributes { get; init; }
}

public sealed record SessionGenAIStatsDto
{
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
    public int TotalTokens { get; init; }
    public double TotalCostUsd { get; init; }
    
    /// <summary>Number of GenAI spans</summary>
    public int RequestCount { get; init; }
    
    public int ToolCallCount { get; init; }
    
    /// <summary>Unique models used</summary>
    public List<string> Models { get; init; } = [];
    
    /// <summary>Unique providers used</summary>
    public List<string> Providers { get; init; } = [];
    
    /// <summary>Most frequently used model</summary>
    public string? PrimaryModel { get; init; }
}

// =============================================================================
// RESPONSES
// =============================================================================

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

// =============================================================================
// REALTIME
// =============================================================================

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
