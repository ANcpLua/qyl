using qyl.collector.Auth;
using qyl.collector.BuildFailures;
using qyl.collector.Dashboards;
using qyl.collector.Insights;
using qyl.collector.Meta;
using qyl.collector.Search;
using qyl.protocol.Copilot;

namespace qyl.collector;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    WriteIndented = false)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(AuthCheckResponse))]
[JsonSerializable(typeof(StorageStats))]
[JsonSerializable(typeof(GenAiStats))]
[JsonSerializable(typeof(ConsoleLogEntry))]
[JsonSerializable(typeof(ConsoleLogEntry[]))]
[JsonSerializable(typeof(ConsoleIngestRequest))]
[JsonSerializable(typeof(ConsoleIngestBatch))]
[JsonSerializable(typeof(TelemetryEventDto))]
[JsonSerializable(typeof(TelemetryMessage))]
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
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(SpanBatch))]
[JsonSerializable(typeof(SpanStorageRow))]
[JsonSerializable(typeof(List<SpanStorageRow>))]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "DictionaryStringString")]
[JsonSerializable(typeof(OtlpExportTraceServiceRequest))]
[JsonSerializable(typeof(OtlpResourceSpans))]
[JsonSerializable(typeof(OtlpResource))]
[JsonSerializable(typeof(OtlpScopeSpans))]
[JsonSerializable(typeof(OtlpSpan))]
[JsonSerializable(typeof(OtlpStatus))]
[JsonSerializable(typeof(OtlpKeyValue))]
[JsonSerializable(typeof(OtlpAnyValue))]
[JsonSerializable(typeof(OtlpExportLogsServiceRequest))]
[JsonSerializable(typeof(OtlpResourceLogs))]
[JsonSerializable(typeof(OtlpScopeLogs))]
[JsonSerializable(typeof(OtlpLogRecord))]
[JsonSerializable(typeof(LogStorageRow))]
[JsonSerializable(typeof(List<LogStorageRow>))]
[JsonSerializable(typeof(string[]))]
// Ring buffer response types
[JsonSerializable(typeof(RecentSpansResponse))]
[JsonSerializable(typeof(TraceFromMemoryResponse))]
[JsonSerializable(typeof(SessionSpansFromMemoryResponse))]
[JsonSerializable(typeof(BufferStatsResponse))]
// Telemetry management types
[JsonSerializable(typeof(ClearTelemetryResponse))]
// Insights materializer types
[JsonSerializable(typeof(InsightsResponse))]
[JsonSerializable(typeof(InsightTierStatus))]
[JsonSerializable(typeof(IReadOnlyList<InsightTierStatus>))]
// Dashboard types
[JsonSerializable(typeof(DashboardDefinition))]
[JsonSerializable(typeof(DashboardData))]
[JsonSerializable(typeof(DashboardWidget))]
[JsonSerializable(typeof(StatCardData))]
[JsonSerializable(typeof(TimeSeriesPoint))]
[JsonSerializable(typeof(TopNRow))]
[JsonSerializable(typeof(IReadOnlyList<DashboardDefinition>))]
[JsonSerializable(typeof(IReadOnlyList<DashboardWidget>))]
[JsonSerializable(typeof(IReadOnlyList<TopNRow>))]
[JsonSerializable(typeof(IReadOnlyList<TimeSeriesPoint>))]
// Copilot types (for request body deserialization)
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(WorkflowRunRequest))]
[JsonSerializable(typeof(CopilotAuthStatus))]
[JsonSerializable(typeof(StreamUpdate))]
// Meta endpoint types
[JsonSerializable(typeof(MetaResponse))]
[JsonSerializable(typeof(MetaBuild))]
[JsonSerializable(typeof(MetaCapabilities))]
[JsonSerializable(typeof(MetaStatus))]
[JsonSerializable(typeof(MetaLinks))]
[JsonSerializable(typeof(MetaPorts))]
// Error engine types
[JsonSerializable(typeof(ErrorStatusUpdate))]
[JsonSerializable(typeof(ErrorRow))]
[JsonSerializable(typeof(IReadOnlyList<ErrorRow>))]
[JsonSerializable(typeof(ErrorStats))]
[JsonSerializable(typeof(ErrorCategoryStat))]
[JsonSerializable(typeof(IReadOnlyList<ErrorCategoryStat>))]
[JsonSerializable(typeof(BuildFailureIngestRequest))]
[JsonSerializable(typeof(BuildFailureResponse))]
[JsonSerializable(typeof(BuildFailureResponse[]))]
// Search types
[JsonSerializable(typeof(SearchQuery))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(IReadOnlyList<SearchResult>))]
[JsonSerializable(typeof(SearchSuggestion))]
[JsonSerializable(typeof(IReadOnlyList<SearchSuggestion>))]
public partial class QylSerializerContext : JsonSerializerContext;
