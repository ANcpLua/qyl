using System.Text.Json.Serialization;
using qyl.collector.Auth;
using qyl.collector.ConsoleBridge;
using qyl.collector.Contracts;
using qyl.collector.Ingestion;
using qyl.collector.Mcp;
using qyl.collector.Realtime;
using qyl.collector.Storage;
using TelemetryEventDto = qyl.collector.Realtime.TelemetryEventDto;

namespace qyl.collector;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(AuthCheckResponse))]
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
[JsonSerializable(typeof(ConsoleLogEntry))]
[JsonSerializable(typeof(ConsoleLogEntry[]))]
[JsonSerializable(typeof(ConsoleIngestRequest))]
[JsonSerializable(typeof(ConsoleIngestBatch))]
[JsonSerializable(typeof(TelemetryEventDto))]
[JsonSerializable(typeof(TelemetryMessage))]
[JsonSerializable(typeof(SseConnectedEvent))]
[JsonSerializable(typeof(SpanDto))]
[JsonSerializable(typeof(SpanDto[]))]
[JsonSerializable(typeof(List<SpanDto>))]
[JsonSerializable(typeof(GenAiSpanDataDto))]
[JsonSerializable(typeof(SpanEventDto))]
[JsonSerializable(typeof(SpanLinkDto))]
[JsonSerializable(typeof(SessionDto))]
[JsonSerializable(typeof(SessionDto[]))]
[JsonSerializable(typeof(List<SessionDto>))]
[JsonSerializable(typeof(SessionGenAiStatsDto))]
[JsonSerializable(typeof(SessionListResponseDto))]
[JsonSerializable(typeof(SpanListResponseDto))]
[JsonSerializable(typeof(TraceResponseDto))]
[JsonSerializable(typeof(SpanBatchDto))]
[JsonSerializable(typeof(Contracts.TelemetryEventDto), TypeInfoPropertyName = "ContractsTelemetryEventDto")]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(FeedbackResponse))]
[JsonSerializable(typeof(SpanBatch))]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "DictionaryStringString")]
[JsonSerializable(typeof(OtlpExportTraceServiceRequest))]
[JsonSerializable(typeof(OtlpResourceSpans))]
[JsonSerializable(typeof(OtlpResource))]
[JsonSerializable(typeof(OtlpScopeSpans))]
[JsonSerializable(typeof(OtlpSpan))]
[JsonSerializable(typeof(OtlpStatus))]
[JsonSerializable(typeof(OtlpKeyValue))]
[JsonSerializable(typeof(OtlpAnyValue))]
public partial class QylSerializerContext : JsonSerializerContext;

public sealed record ConsoleIngestBatch(List<ConsoleIngestRequest> Logs);

public sealed record FeedbackResponse(object[] Feedback);

public sealed record HealthResponse(string Status);

public sealed record AuthCheckResponse(bool Authenticated);

public sealed record SseConnectedEvent(string ConnectionId);

public sealed record ErrorResponse(string Error, string? Message = null);
