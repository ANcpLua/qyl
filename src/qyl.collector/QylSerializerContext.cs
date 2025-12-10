// qyl.collector - JSON Serializer Context
// Source-generated JSON serialization for Native AOT compatibility
//
// IMPORTANT: Only include types that are actually serialized by endpoints.
// The DTO types (Contracts namespace) are the canonical API response shapes.

using System.Text.Json.Serialization;
using qyl.collector.Auth;
using qyl.collector.ConsoleBridge;
using qyl.collector.Contracts;
using qyl.collector.Ingestion;
using qyl.collector.Mcp;
using qyl.collector.Query;
using qyl.collector.Realtime;
using qyl.collector.Storage;

namespace qyl.collector;

/// <summary>
/// Source-generated JSON serializer context for AOT compatibility.
/// All types that need JSON serialization must be listed here.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]

// =============================================================================
// AUTH
// =============================================================================
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(AuthCheckResponse))]

// =============================================================================
// STORAGE (internal - used by DuckDB queries, not API responses)
// =============================================================================
[JsonSerializable(typeof(SpanRecord))]
[JsonSerializable(typeof(SpanRecord[]))]
[JsonSerializable(typeof(StorageStats))]
[JsonSerializable(typeof(GenAiStats))]

// =============================================================================
// MCP
// =============================================================================
[JsonSerializable(typeof(McpToolCall))]
[JsonSerializable(typeof(McpResponse))]
[JsonSerializable(typeof(McpContent))]
[JsonSerializable(typeof(McpManifest))]
[JsonSerializable(typeof(McpTool))]
[JsonSerializable(typeof(McpTool[]))]

// =============================================================================
// CONSOLE BRIDGE
// =============================================================================
[JsonSerializable(typeof(ConsoleLogEntry))]
[JsonSerializable(typeof(ConsoleLogEntry[]))]
[JsonSerializable(typeof(ConsoleIngestRequest))]
[JsonSerializable(typeof(ConsoleIngestBatch))]

// =============================================================================
// SSE REALTIME (internal streaming types)
// =============================================================================
[JsonSerializable(typeof(Realtime.TelemetryEventDto))]
[JsonSerializable(typeof(TelemetryMessage))]
[JsonSerializable(typeof(SseConnectedEvent))]

// =============================================================================
// API RESPONSE DTOs (from Contracts - aligned with OpenAPI spec)
// These are the ONLY types that should be returned by /api/v1/* endpoints
// =============================================================================

// Span types
[JsonSerializable(typeof(SpanDto))]
[JsonSerializable(typeof(SpanDto[]))]
[JsonSerializable(typeof(List<SpanDto>))]
[JsonSerializable(typeof(GenAISpanDataDto))]
[JsonSerializable(typeof(Contracts.SpanEventDto))]
[JsonSerializable(typeof(SpanLinkDto))]

// Session types
[JsonSerializable(typeof(SessionDto))]
[JsonSerializable(typeof(SessionDto[]))]
[JsonSerializable(typeof(List<SessionDto>))]
[JsonSerializable(typeof(SessionGenAIStatsDto))]

// Response wrappers
[JsonSerializable(typeof(SessionListResponseDto))]
[JsonSerializable(typeof(SpanListResponseDto))]
[JsonSerializable(typeof(TraceResponseDto))]
[JsonSerializable(typeof(SpanBatchDto))]

// Note: Contracts.TelemetryEventDto needs unique name to avoid conflict with Realtime.TelemetryEventDto
[JsonSerializable(typeof(Contracts.TelemetryEventDto), TypeInfoPropertyName = "ContractsTelemetryEventDto")]

// =============================================================================
// GENERIC/UTILITY
// =============================================================================
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(FeedbackResponse))]

// =============================================================================
// INGESTION TYPES
// =============================================================================
[JsonSerializable(typeof(SpanBatch))]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "DictionaryStringString")]

// OTLP types for /v1/traces endpoint
[JsonSerializable(typeof(OtlpExportTraceServiceRequest))]
[JsonSerializable(typeof(OtlpResourceSpans))]
[JsonSerializable(typeof(OtlpResource))]
[JsonSerializable(typeof(OtlpScopeSpans))]
[JsonSerializable(typeof(OtlpSpan))]
[JsonSerializable(typeof(OtlpStatus))]
[JsonSerializable(typeof(OtlpKeyValue))]
[JsonSerializable(typeof(OtlpAnyValue))]

public partial class QylSerializerContext : JsonSerializerContext;

// =============================================================================
// SIMPLE RESPONSE RECORDS (defined here for simplicity)
// =============================================================================

/// <summary>Console batch request for /api/v1/console POST</summary>
public sealed record ConsoleIngestBatch(List<ConsoleIngestRequest> Logs);

/// <summary>Feedback response for /api/v1/sessions/{id}/feedback</summary>
public sealed record FeedbackResponse(object[] Feedback);

/// <summary>Health check response</summary>
public sealed record HealthResponse(string Status);

/// <summary>Auth check response for /api/auth/check</summary>
public sealed record AuthCheckResponse(bool Authenticated);

/// <summary>SSE connected event</summary>
public sealed record SseConnectedEvent(string ConnectionId);

/// <summary>Generic error response</summary>
public sealed record ErrorResponse(string Error, string? Message = null);

// =============================================================================
// REMOVED - These were duplicates of the DTO types above:
// - SessionsResponse (replaced by SessionListResponseDto)
// - SessionListResponse (replaced by SessionListResponseDto)
// - SpansResponse (replaced by SpanListResponseDto)
// - TraceResponse (replaced by TraceResponseDto)
// =============================================================================
