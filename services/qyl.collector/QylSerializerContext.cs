using Qyl.Collector.Meta;
using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Otel;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Logs;
using Qyl.Api.Contracts.OTel.Profiles;
using Qyl.Api.Contracts.OTel.Traces;
using ContractGenAiStats = Qyl.Api.Contracts.Domains.AI.GenAi.GenAiStats;
using ContractAttribute = Qyl.Api.Contracts.Common.Attribute;
using Resource = Qyl.Api.Contracts.OTel.Resource.Resource;

namespace Qyl.Collector;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    WriteIndented = false)]
[JsonSerializable(typeof(TelemetryStats))]
[JsonSerializable(typeof(ContractGenAiStats))]
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
[JsonSerializable(typeof(LogRecord))]
[JsonSerializable(typeof(LogRecord[]))]
[JsonSerializable(typeof(CursorPageLogRecord))]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(Profile[]))]
[JsonSerializable(typeof(List<Profile>))]
[JsonSerializable(typeof(SessionEntity))]
[JsonSerializable(typeof(SessionEntity[]))]
[JsonSerializable(typeof(List<SessionEntity>))]
[JsonSerializable(typeof(SessionGenAiUsage))]
[JsonSerializable(typeof(CursorPageSessionEntity))]
[JsonSerializable(typeof(Resource))]
[JsonSerializable(typeof(InstrumentationScope))]
[JsonSerializable(typeof(ContractAttribute))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(SpanBatch))]
[JsonSerializable(typeof(SpanStorageRow))]
[JsonSerializable(typeof(List<SpanStorageRow>))]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "DictionaryStringString")]
[JsonSerializable(typeof(OtlpResource))]
[JsonSerializable(typeof(OtlpKeyValue))]
[JsonSerializable(typeof(OtlpAnyValue))]
[JsonSerializable(typeof(OtlpArrayValue))]
[JsonSerializable(typeof(OtlpKeyValueList))]
[JsonSerializable(typeof(OtlpExportProfilesServiceRequest))]
[JsonSerializable(typeof(OtlpResourceProfiles))]
[JsonSerializable(typeof(OtlpScopeProfiles))]
[JsonSerializable(typeof(OtlpProfile))]
[JsonSerializable(typeof(OtlpValueType))]
[JsonSerializable(typeof(OtlpProfileSample))]
[JsonSerializable(typeof(OtlpProfileFunction))]
[JsonSerializable(typeof(OtlpProfileLocation))]
[JsonSerializable(typeof(OtlpProfileLine))]
[JsonSerializable(typeof(OtlpProfileMapping))]
[JsonSerializable(typeof(OtlpProfileLink))]
[JsonSerializable(typeof(OtlpProfileStack))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(ClearTelemetryResponse))]
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
[JsonSerializable(typeof(ErrorRow))]
[JsonSerializable(typeof(IReadOnlyList<ErrorRow>))]
[JsonSerializable(typeof(ErrorStats))]
[JsonSerializable(typeof(ErrorCategoryStat))]
[JsonSerializable(typeof(IReadOnlyList<ErrorCategoryStat>))]
public partial class QylSerializerContext : JsonSerializerContext;
