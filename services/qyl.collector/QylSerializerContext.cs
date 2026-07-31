using Qyl.Api.Contracts;
using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Logs;
using Qyl.Api.Contracts.OTel.Traces;
using Qyl.Api.Contracts.Streaming;
using Qyl.Api.Contracts.Workflow;
using ContractInternalServerError = Qyl.Api.Contracts.Common.Errors.InternalServerError;
using ContractAttribute = Qyl.Api.Contracts.Common.Attribute;
using Resource = Qyl.Api.Contracts.OTel.Resource.Resource;

namespace Qyl.Collector;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString |
                     JsonNumberHandling.AllowNamedFloatingPointLiterals,
    WriteIndented = false)]
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
[JsonSerializable(typeof(LogBodyString))]
[JsonSerializable(typeof(LogBodyKvList))]
[JsonSerializable(typeof(LogBodyArray))]
[JsonSerializable(typeof(LogBodyBytes))]
[JsonSerializable(typeof(CursorPageLogRecord))]
[JsonSerializable(typeof(SessionEntity))]
[JsonSerializable(typeof(SessionEntity[]))]
[JsonSerializable(typeof(List<SessionEntity>))]
[JsonSerializable(typeof(SessionGenAiUsage))]
[JsonSerializable(typeof(SessionStats))]
[JsonSerializable(typeof(CursorPageSessionEntity))]
[JsonSerializable(typeof(Resource))]
[JsonSerializable(typeof(EntityRef))]
[JsonSerializable(typeof(EntityRef[]))]
[JsonSerializable(typeof(InstrumentationScope))]
[JsonSerializable(typeof(ContractAttribute))]
[JsonSerializable(typeof(ContractAttribute[]))]
[JsonSerializable(typeof(AttributeBytesValue))]
[JsonSerializable(typeof(AttributeIntValue))]
[JsonSerializable(typeof(AttributeDoubleValue))]
[JsonSerializable(typeof(AttributeKeyValueListValue))]
[JsonSerializable(typeof(NotFoundError))]
[JsonSerializable(typeof(ValidationError))]
[JsonSerializable(typeof(ValidationErrorDetail))]
[JsonSerializable(typeof(ConflictError))]
[JsonSerializable(typeof(UnauthorizedError))]
[JsonSerializable(typeof(ServiceUnavailableError))]
[JsonSerializable(typeof(LogStreamEvent))]
[JsonSerializable(typeof(HeartbeatEvent))]
[JsonSerializable(typeof(WorkflowRun))]
[JsonSerializable(typeof(WorkflowRun[]))]
[JsonSerializable(typeof(WorkflowRunPage))]
[JsonSerializable(typeof(WorkflowRunCreateRequest))]
[JsonSerializable(typeof(WorkflowEventAppend))]
[JsonSerializable(typeof(WorkflowEventAppend[]))]
[JsonSerializable(typeof(WorkflowEventBatchAppendRequest))]
[JsonSerializable(typeof(WorkflowEventBatchAppendResponse))]
[JsonSerializable(typeof(WorkflowJournalEvent))]
[JsonSerializable(typeof(WorkflowJournalEvent[]))]
[JsonSerializable(typeof(WorkflowEventPage))]
[JsonSerializable(typeof(WorkflowGraphNode))]
[JsonSerializable(typeof(WorkflowGraphNode[]))]
[JsonSerializable(typeof(WorkflowGraphEdge))]
[JsonSerializable(typeof(WorkflowGraphEdge[]))]
[JsonSerializable(typeof(WorkflowGraphStatistics))]
[JsonSerializable(typeof(WorkflowGraphSnapshot))]
[JsonSerializable(typeof(WorkflowProjectionStatus))]
[JsonSerializable(typeof(CommittedWorkflowProjectionStatus))]
[JsonSerializable(typeof(RebuildingWorkflowProjectionStatus))]
[JsonSerializable(typeof(UnavailableWorkflowProjectionStatus))]
[JsonSerializable(typeof(CorruptWorkflowProjectionStatus))]
[JsonSerializable(typeof(WorkflowCursorError))]
[JsonSerializable(typeof(WorkflowRunDeletedError))]
[JsonSerializable(typeof(WorkflowProjectionUnavailableError))]
[JsonSerializable(typeof(WorkflowProjectionCorruptError))]
[JsonSerializable(typeof(WorkflowEdgeProvenance))]
[JsonSerializable(typeof(RecordedWorkflowEdgeProvenance))]
[JsonSerializable(typeof(DerivedWorkflowEdgeProvenance))]
[JsonSerializable(typeof(WorkflowContent))]
[JsonSerializable(typeof(WorkflowControlRequest))]
[JsonSerializable(typeof(WorkflowControlCommand))]
[JsonSerializable(typeof(WorkflowControlCommand[]))]
[JsonSerializable(typeof(WorkflowControlCommandPage))]
[JsonSerializable(typeof(WorkflowControlStatusUpdateRequest))]
[JsonSerializable(typeof(WorkflowHeartbeatEvent))]
[JsonSerializable(typeof(WorkflowCursorGapEvent))]
[JsonSerializable(typeof(ContractInternalServerError), TypeInfoPropertyName = "ContractInternalServerError")]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(JsonElement))]
internal partial class QylSerializerContext : JsonSerializerContext;
