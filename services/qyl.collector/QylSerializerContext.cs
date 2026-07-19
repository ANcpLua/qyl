using Qyl.Api.Contracts;
using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Logs;
using Qyl.Api.Contracts.OTel.Metrics;
using Qyl.Api.Contracts.OTel.Profiles;
using Qyl.Api.Contracts.OTel.Traces;
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
[JsonSerializable(typeof(MetricPoint))]
[JsonSerializable(typeof(GaugeMetricPoint))]
[JsonSerializable(typeof(SumMetricPoint))]
[JsonSerializable(typeof(HistogramMetricPoint))]
[JsonSerializable(typeof(ExponentialHistogramMetricPoint))]
[JsonSerializable(typeof(SummaryMetricPoint))]
[JsonSerializable(typeof(MetricNumberValue))]
[JsonSerializable(typeof(MetricIntegerValue))]
[JsonSerializable(typeof(MetricDoubleValue))]
[JsonSerializable(typeof(MetricExemplar))]
[JsonSerializable(typeof(ExponentialHistogramBuckets))]
[JsonSerializable(typeof(SummaryQuantileValue))]
[JsonSerializable(typeof(CursorPageMetricPoint))]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(Profile[]))]
[JsonSerializable(typeof(List<Profile>))]
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
[JsonSerializable(typeof(UnauthorizedError))]
[JsonSerializable(typeof(ServiceUnavailableError))]
[JsonSerializable(typeof(LogStreamEvent))]
[JsonSerializable(typeof(HeartbeatEvent))]
[JsonSerializable(typeof(ContractInternalServerError), TypeInfoPropertyName = "ContractInternalServerError")]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(JsonElement))]
internal partial class QylSerializerContext : JsonSerializerContext;
