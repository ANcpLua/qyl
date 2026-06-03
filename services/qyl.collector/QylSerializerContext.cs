using Qyl.Api.Contracts;
using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Otel;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Logs;
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
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
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
[JsonSerializable(typeof(CursorPageLogRecord))]
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
[JsonSerializable(typeof(InstrumentationScope))]
[JsonSerializable(typeof(ContractAttribute))]
[JsonSerializable(typeof(NotFoundError))]
[JsonSerializable(typeof(ValidationError))]
[JsonSerializable(typeof(ValidationErrorDetail))]
[JsonSerializable(typeof(UnauthorizedError))]
[JsonSerializable(typeof(LogStreamEvent))]
[JsonSerializable(typeof(HeartbeatEvent))]
[JsonSerializable(typeof(ContractInternalServerError), TypeInfoPropertyName = "ContractInternalServerError")]
[JsonSerializable(typeof(string[]))]
internal partial class QylSerializerContext : JsonSerializerContext;
