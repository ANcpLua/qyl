namespace Qyl.Collector;

using Artifacts;
using Cost;
using Dashboards;
using Health;
using Insights;
using Meta;
using Qyl.Contracts.Copilot;
using Search;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    WriteIndented = false)]
[JsonSerializable(typeof(StorageStats))]
[JsonSerializable(typeof(GenAiStats))]
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
[JsonSerializable(typeof(HealthUiResponse))]
[JsonSerializable(typeof(ComponentHealth))]
[JsonSerializable(typeof(IReadOnlyList<ComponentHealth>))]
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
// Profile OTLP types (for ProfileDataJson blob serialization)
[JsonSerializable(typeof(OtlpProfile))]
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
[JsonSerializable(typeof(ByokLlmConfig))]
[JsonSerializable(typeof(WorkflowRunRequest))]
[JsonSerializable(typeof(CopilotAuthStatus))]
[JsonSerializable(typeof(StreamUpdate))]
[JsonSerializable(typeof(AgentRunAudit))]
[JsonSerializable(typeof(AgentDecision))]
[JsonSerializable(typeof(AgentEvidenceLink))]
// Agent run endpoint payloads
[JsonSerializable(typeof(AgentRunRecord))]
[JsonSerializable(typeof(ToolCallRecord))]
[JsonSerializable(typeof(AgentDecisionRecord))]
[JsonSerializable(typeof(List<AgentRunRecord>))]
[JsonSerializable(typeof(List<ToolCallRecord>))]
[JsonSerializable(typeof(List<AgentDecisionRecord>))]
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
// Search types
[JsonSerializable(typeof(SearchQuery))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(IReadOnlyList<SearchResult>))]
[JsonSerializable(typeof(SearchSuggestion))]
[JsonSerializable(typeof(IReadOnlyList<SearchSuggestion>))]
// Artifact types
[JsonSerializable(typeof(ArtifactCreateRequest))]
[JsonSerializable(typeof(ArtifactResponse))]
// Cost engine types
[JsonSerializable(typeof(PricingOverrideRequest))]
public partial class QylSerializerContext : JsonSerializerContext;
