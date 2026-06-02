using Qyl.Collector.Artifacts;
using Qyl.Collector.Cost;
using Qyl.Collector.Health;
using Qyl.Collector.Insights;
using Qyl.Collector.Meta;
using Qyl.Collector.Search;
using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Traces;
using ContractAttribute = Qyl.Api.Contracts.Common.Attribute;
using Resource = Qyl.Api.Contracts.OTel.Resource.Resource;

namespace Qyl.Collector;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    WriteIndented = false)]
[JsonSerializable(typeof(StorageStats))]
[JsonSerializable(typeof(GenAiStats))]
[JsonSerializable(typeof(TelemetryEventDto))]
[JsonSerializable(typeof(TelemetryMessage))]
[JsonSerializable(typeof(Span))]
[JsonSerializable(typeof(Span[]))]
[JsonSerializable(typeof(List<Span>))]
[JsonSerializable(typeof(SpanEvent))]
[JsonSerializable(typeof(SpanLink))]
[JsonSerializable(typeof(SpanStatus))]
[JsonSerializable(typeof(Qyl.Api.Contracts.OTel.Traces.Trace), TypeInfoPropertyName = "OtelTrace")]
[JsonSerializable(typeof(CursorPageSpan))]
[JsonSerializable(typeof(CursorPageTrace))]
[JsonSerializable(typeof(SessionEntity))]
[JsonSerializable(typeof(SessionEntity[]))]
[JsonSerializable(typeof(List<SessionEntity>))]
[JsonSerializable(typeof(SessionGenAiUsage))]
[JsonSerializable(typeof(CursorPageSessionEntity))]
[JsonSerializable(typeof(Resource))]
[JsonSerializable(typeof(InstrumentationScope))]
[JsonSerializable(typeof(ContractAttribute))]
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
[JsonSerializable(typeof(OtlpProfile))]
[JsonSerializable(typeof(OtlpResourceLogs))]
[JsonSerializable(typeof(OtlpScopeLogs))]
[JsonSerializable(typeof(OtlpLogRecord))]
[JsonSerializable(typeof(LogStorageRow))]
[JsonSerializable(typeof(List<LogStorageRow>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(ClearTelemetryResponse))]
[JsonSerializable(typeof(InsightsResponse))]
[JsonSerializable(typeof(InsightTierStatus))]
[JsonSerializable(typeof(IReadOnlyList<InsightTierStatus>))]
[JsonSerializable(typeof(AgentRunRecord))]
[JsonSerializable(typeof(ToolCallRecord))]
[JsonSerializable(typeof(AgentDecisionRecord))]
[JsonSerializable(typeof(List<AgentRunRecord>))]
[JsonSerializable(typeof(List<ToolCallRecord>))]
[JsonSerializable(typeof(List<AgentDecisionRecord>))]
[JsonSerializable(typeof(MetaResponse))]
[JsonSerializable(typeof(MetaBuild))]
[JsonSerializable(typeof(MetaCapabilities))]
[JsonSerializable(typeof(MetaStatus))]
[JsonSerializable(typeof(MetaLinks))]
[JsonSerializable(typeof(MetaPorts))]
[JsonSerializable(typeof(ErrorStatusUpdate))]
[JsonSerializable(typeof(ErrorRow))]
[JsonSerializable(typeof(IReadOnlyList<ErrorRow>))]
[JsonSerializable(typeof(ErrorStats))]
[JsonSerializable(typeof(ErrorCategoryStat))]
[JsonSerializable(typeof(IReadOnlyList<ErrorCategoryStat>))]
[JsonSerializable(typeof(SearchQuery))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(IReadOnlyList<SearchResult>))]
[JsonSerializable(typeof(SearchSuggestion))]
[JsonSerializable(typeof(IReadOnlyList<SearchSuggestion>))]
[JsonSerializable(typeof(ArtifactCreateRequest))]
[JsonSerializable(typeof(ArtifactResponse))]
[JsonSerializable(typeof(PricingOverrideRequest))]
public partial class QylSerializerContext : JsonSerializerContext;
