using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Enums;
using Qyl.Api.Contracts.OTel.Traces;
using ContractAttribute = Qyl.Api.Contracts.Common.Attribute;
using Resource = Qyl.Api.Contracts.OTel.Resource.Resource;
using TraceContract = Qyl.Api.Contracts.OTel.Traces.Trace;

namespace Qyl.Collector.Mapping;

public static class SpanMapper
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    public static Span ToContract(SpanStorageRow record, string serviceName, string? serviceVersion = null) =>
        ToContractCore(
            record.TraceId, record.SpanId, record.ParentSpanId, record.SessionId,
            record.Name, record.Kind, record.StatusCode, record.StatusMessage,
            record.StartTimeUnixNano, record.EndTimeUnixNano, record.DurationNs,
            serviceName, serviceVersion,
            record.AttributesJson, record.SchemaUrl);

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    public static List<Span> ToContracts(
        IEnumerable<SpanStorageRow> records,
        Func<SpanStorageRow, (string ServiceName, string? ServiceVersion)> serviceResolver) =>
    [
        .. records.Select(r =>
        {
            var (serviceName, serviceVersion) = serviceResolver(r);
            return ToContract(r, serviceName, serviceVersion);
        })
    ];

    public static TraceContract ToTrace(string traceId, IReadOnlyList<Span> spans)
    {
        var rootSpan = spans.FirstOrDefault(static s => s.ParentSpanId is null);
        var start = spans.Count is 0 ? 0UL : spans.Min(static s => s.StartTimeUnixNano);
        var end = spans.Count is 0 ? 0UL : spans.Max(static s => s.EndTimeUnixNano);
        var duration = end >= start ? end - start : 0UL;

        return new TraceContract
        {
            TraceId = traceId,
            Spans = spans,
            RootSpan = rootSpan,
            SpanCount = spans.Count,
            DurationNs = duration,
            StartTime = QylTimeConversions.NanosToDateTimeOffset(start),
            EndTime = QylTimeConversions.NanosToDateTimeOffset(end),
            Services = [.. spans.Select(static s => s.Resource.ServiceName).Distinct(StringComparer.Ordinal)],
            HasError = spans.Any(static s => s.Status.Code is SpanStatusCode.Error)
        };
    }

    private static SpanKind MapSpanKind(byte kind) =>
        kind switch
        {
            1 => SpanKind.Internal,
            2 => SpanKind.Server,
            3 => SpanKind.Client,
            4 => SpanKind.Producer,
            5 => SpanKind.Consumer,
            _ => SpanKind.Unspecified
        };

    private static SpanStatusCode MapStatus(byte statusCode) =>
        statusCode switch
        {
            1 => SpanStatusCode.Ok,
            2 => SpanStatusCode.Error,
            _ => SpanStatusCode.Unset
        };

    [RequiresUnreferencedCode("Deserializes dynamic OTLP attribute values")]
    [RequiresDynamicCode("Deserializes dynamic OTLP attribute values")]
    private static IReadOnlyList<ContractAttribute>? ParseAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var attributes = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, s_jsonOptions);
            return attributes is null
                ? null
                : [.. attributes.Select(static kvp => new ContractAttribute { Key = kvp.Key, Value = kvp.Value ?? "" })];
        }
        catch
        {
            return null;
        }
    }

    [RequiresUnreferencedCode("Deserializes dynamic OTLP span attributes")]
    [RequiresDynamicCode("Deserializes dynamic OTLP span attributes")]
    private static Span ToContractCore(
        string traceId, string spanId, string? parentSpanId, string? sessionId,
        string name, byte kind, byte statusCode, string? statusMessage,
        ulong startTimeUnixNano, ulong endTimeUnixNano, ulong durationNs,
        string serviceName, string? serviceVersion,
        string? attributesJson, string? schemaUrl)
    {
        _ = sessionId;
        _ = durationNs;
        var attributes = ParseAttributes(attributesJson);

        return new Span
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            Name = name,
            Kind = MapSpanKind(kind),
            StartTimeUnixNano = startTimeUnixNano,
            EndTimeUnixNano = endTimeUnixNano,
            Attributes = attributes,
            Events = [],
            Links = [],
            Status = new SpanStatus
            {
                Code = MapStatus(statusCode),
                Message = statusMessage
            },
            Resource = new Resource
            {
                ServiceName = string.IsNullOrWhiteSpace(serviceName) ? "unknown" : serviceName,
                ServiceVersion = serviceVersion,
                Attributes = attributes
            },
            InstrumentationScope = schemaUrl is null
                ? null
                : new InstrumentationScope { ScopeName = schemaUrl, ScopeVersion = serviceVersion }
        };
    }
}

internal static class AttributeParsing
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? ParseNullableLong(string? value) =>
        string.IsNullOrEmpty(value) ? null : value.TryParseInt64();
}

public static class SessionMapper
{
    public static SessionEntity ToContract(SessionQueryRow summary)
    {
        var startTime = AsUtcOffset(summary.StartTime);
        var lastActivity = AsUtcOffset(summary.LastActivity);
        var isActive = (TimeProvider.System.GetUtcNow() - lastActivity).TotalMinutes < 5;

        return new SessionEntity
        {
            SessionId = summary.SessionId,
            StartTime = startTime,
            EndTime = isActive ? null : lastActivity,
            DurationMs = summary.DurationMs,
            SpanCount = ToInt32(summary.SpanCount),
            TraceCount = ToInt32(summary.TraceCount),
            ErrorCount = ToInt32(summary.ErrorCount),
            Services = [.. summary.Services],
            State = isActive ? SessionState.Active : SessionState.Ended,
            GenaiUsage = new SessionGenAiUsage
            {
                RequestCount = ToInt32(summary.GenAiRequestCount),
                TotalInputTokens = summary.InputTokens,
                TotalOutputTokens = summary.OutputTokens,
                ModelsUsed = [.. summary.Models],
                ProvidersUsed = ExtractProviders(summary),
                EstimatedCostUsd = summary.TotalCostUsd
            }
        };
    }

    private static List<SessionEntity> ToContracts(IEnumerable<SessionQueryRow> summaries) =>
        [.. summaries.Select(ToContract)];

    public static CursorPageSessionEntity ToPage(
        IEnumerable<SessionQueryRow> summaries,
        int total,
        bool hasMore)
    {
        _ = total;
        return new CursorPageSessionEntity { Items = ToContracts(summaries), HasMore = hasMore };
    }

    private static List<string> ExtractProviders(SessionQueryRow summary)
    {
        var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in summary.Models)
        {
            var provider = InferProvider(model);
            if (provider is not null)
                providers.Add(provider);
        }

        return [.. providers];
    }

    private static string? InferProvider(string model) =>
        model switch
        {
            _ when model.StartsWithIgnoreCase("gpt") => "openai",
            _ when model.StartsWithIgnoreCase("o1") => "openai",
            _ when model.StartsWithIgnoreCase("claude") => "anthropic",
            _ when model.StartsWithIgnoreCase("gemini") => "google",
            _ when model.StartsWithIgnoreCase("llama") => "meta",
            _ when model.StartsWithIgnoreCase("mistral") => "mistral",
            _ when model.StartsWithIgnoreCase("command") => "cohere",
            _ => null
        };

    private static int ToInt32(long value) =>
        value switch
        {
            > int.MaxValue => int.MaxValue,
            < int.MinValue => int.MinValue,
            _ => (int)value
        };

    private static DateTimeOffset AsUtcOffset(DateTime timestamp) =>
        new(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
}
