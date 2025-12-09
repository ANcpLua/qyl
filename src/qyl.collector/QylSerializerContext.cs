// qyl.collector - JSON Serializer Context
// Source-generated JSON serialization for Native AOT compatibility

using System.Text.Json.Serialization;
using qyl.collector.Auth;
using qyl.collector.ConsoleBridge;
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
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(SpanBatch))]
[JsonSerializable(typeof(SpanRecord))]
[JsonSerializable(typeof(SpanRecord[]))]
[JsonSerializable(typeof(StorageStats))]
[JsonSerializable(typeof(GenAiStats))]
[JsonSerializable(typeof(McpToolCall))]
[JsonSerializable(typeof(McpResponse))]
[JsonSerializable(typeof(McpContent))]
[JsonSerializable(typeof(McpManifest))]
[JsonSerializable(typeof(McpTool))]
[JsonSerializable(typeof(McpTool[]))]
[JsonSerializable(typeof(SessionsResponse))]
[JsonSerializable(typeof(SpansResponse))]
[JsonSerializable(typeof(TraceResponse))]
[JsonSerializable(typeof(FeedbackResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(AuthCheckResponse))]
[JsonSerializable(typeof(SseConnectedEvent))]
[JsonSerializable(typeof(ErrorResponse))]
// Console bridge types
[JsonSerializable(typeof(ConsoleLogEntry))]
[JsonSerializable(typeof(ConsoleLogEntry[]))]
[JsonSerializable(typeof(ConsoleIngestRequest))]
[JsonSerializable(typeof(ConsoleIngestBatch))]
// Session aggregation types
[JsonSerializable(typeof(SessionSummary))]
[JsonSerializable(typeof(SessionSummary[]))]
[JsonSerializable(typeof(SessionListResponse))]
// SSE streaming types
[JsonSerializable(typeof(TelemetryEventDto))]
[JsonSerializable(typeof(TelemetryMessage))]
public partial class QylSerializerContext : JsonSerializerContext;

// Console batch request
public sealed record ConsoleIngestBatch(List<ConsoleIngestRequest> Logs);

// API Response types for source generation
public sealed record SessionsResponse(IReadOnlyList<SessionSummary> Sessions, int Total, bool HasMore);

public sealed record SessionListResponse(IReadOnlyList<SessionSummary> Sessions, int Total, bool HasMore);
public sealed record SpansResponse(IReadOnlyList<SpanRecord> Spans);
public sealed record TraceResponse(IReadOnlyList<SpanRecord> Spans);
public sealed record FeedbackResponse(object[] Feedback);
public sealed record HealthResponse(string Status);
public sealed record AuthCheckResponse(bool Authenticated);
public sealed record SseConnectedEvent(string ConnectionId);
public sealed record ErrorResponse(string Error, string? Message = null);
