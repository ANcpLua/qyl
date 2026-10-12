using Qyl.Api.Contracts;
using Qyl.Api.Contracts.Annotations;
using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Logs;
using Qyl.Api.Contracts.OTel.Metrics;
using Qyl.Api.Contracts.OTel.Traces;
using Qyl.Api.Contracts.SavedQueries;
using Qyl.Api.Contracts.Streaming;
using ContractInternalServerError = Qyl.Api.Contracts.Common.Errors.InternalServerError;
using ContractAttribute = Qyl.Api.Contracts.Common.Attribute;
using Resource = Qyl.Api.Contracts.OTel.Resource.Resource;

namespace Qyl.Collector;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString |
                     JsonNumberHandling.AllowNamedFloatingPointLiterals,
    // The AttributeObjectValue tag is a plain wire property; producers do not promise its position.
    AllowOutOfOrderMetadataProperties = true,
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
[JsonSerializable(typeof(TraceSummary))]
[JsonSerializable(typeof(LogRecord))]
[JsonSerializable(typeof(LogRecord[]))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Common.AttributeValue))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Common.AttributeObjectValue))]
[JsonSerializable(typeof(CursorPageLogRecord))]
[JsonSerializable(typeof(MetricDescriptor))]
[JsonSerializable(typeof(MetricDescriptor[]))]
[JsonSerializable(typeof(CursorPageMetricDescriptor))]
[JsonSerializable(typeof(MetricSeries))]
[JsonSerializable(typeof(MetricSeries[]))]
[JsonSerializable(typeof(CursorPageMetricSeries))]
[JsonSerializable(typeof(MetricBucket))]
[JsonSerializable(typeof(MetricBucket[]))]
[JsonSerializable(typeof(MetricSeriesResult))]
[JsonSerializable(typeof(MetricSeriesResult[]))]
[JsonSerializable(typeof(MetricQueryResult))]
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
[JsonSerializable(typeof(ForbiddenError))]
[JsonSerializable(typeof(ServiceUnavailableError))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(PreconditionFailedError))]
[JsonSerializable(typeof(PreconditionRequiredError))]
[JsonSerializable(typeof(Annotation))]
[JsonSerializable(typeof(AnnotationTarget))]
[JsonSerializable(typeof(TraceAnnotationTarget))]
[JsonSerializable(typeof(SpanAnnotationTarget))]
[JsonSerializable(typeof(SessionAnnotationTarget))]
[JsonSerializable(typeof(AnnotationCreateRequest))]
[JsonSerializable(typeof(AnnotationUpdateRequest))]
[JsonSerializable(typeof(CursorPageAnnotation))]
[JsonSerializable(typeof(SavedQuery))]
[JsonSerializable(typeof(SavedQueryFilter))]
[JsonSerializable(typeof(SavedTraceQueryFilter))]
[JsonSerializable(typeof(SavedLogQueryFilter))]
[JsonSerializable(typeof(SavedMetricQueryFilter))]
[JsonSerializable(typeof(SavedQueryCreateRequest))]
[JsonSerializable(typeof(SavedQueryUpdateRequest))]
[JsonSerializable(typeof(SavedQueryRunRequest))]
[JsonSerializable(typeof(SavedQueryRunResult))]
[JsonSerializable(typeof(SavedTraceQueryRun))]
[JsonSerializable(typeof(SavedLogQueryRun))]
[JsonSerializable(typeof(SavedMetricQueryRun))]
[JsonSerializable(typeof(CursorPageSavedQuery))]
[JsonSerializable(typeof(LogStreamEvent))]
[JsonSerializable(typeof(HeartbeatEvent))]
[JsonSerializable(typeof(ContractInternalServerError), TypeInfoPropertyName = "ContractInternalServerError")]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(JsonElement))]
internal partial class QylSerializerContext : JsonSerializerContext;
