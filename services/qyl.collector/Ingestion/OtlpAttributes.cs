
namespace Qyl.Collector.Ingestion;


public sealed record OtlpExportTraceServiceRequest
{
    public List<OtlpResourceSpans>? ResourceSpans { get; init; }
}

public sealed record OtlpResourceSpans
{
    public OtlpResource? Resource { get; init; }
    public List<OtlpScopeSpans>? ScopeSpans { get; init; }

    public string? SchemaUrl { get; init; }
}

public sealed record OtlpResource
{
    public List<OtlpKeyValue>? Attributes { get; init; }
}

public sealed record OtlpScopeSpans
{
    public List<OtlpSpan>? Spans { get; init; }

    public string? SchemaUrl { get; init; }
}

public sealed record OtlpSpan
{
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? Name { get; init; }
    public int? Kind { get; init; }

    public ulong StartTimeUnixNano { get; init; }

    public ulong EndTimeUnixNano { get; init; }

    public OtlpStatus? Status { get; init; }
    public List<OtlpKeyValue>? Attributes { get; init; }
}

public sealed record OtlpStatus
{
    public int? Code { get; init; }
    public string? Message { get; init; }
}

public sealed record OtlpKeyValue
{
    public string? Key { get; init; }
    public OtlpAnyValue? Value { get; init; }
}

public sealed record OtlpAnyValue
{
    public string? StringValue { get; init; }
    public long? IntValue { get; init; }
    public double? DoubleValue { get; init; }
    public bool? BoolValue { get; init; }
    public OtlpArrayValue? ArrayValue { get; init; }
    public OtlpKeyValueList? KvlistValue { get; init; }
    public string? BytesValue { get; init; }
}

public sealed record OtlpArrayValue
{
    public List<OtlpAnyValue>? Values { get; init; }
}

public sealed record OtlpKeyValueList
{
    public List<OtlpKeyValue>? Values { get; init; }
}


public sealed record OtlpExportLogsServiceRequest
{
    public List<OtlpResourceLogs>? ResourceLogs { get; init; }
}

public sealed record OtlpResourceLogs
{
    public OtlpResource? Resource { get; init; }
    public List<OtlpScopeLogs>? ScopeLogs { get; init; }
}

public sealed record OtlpScopeLogs
{
    public List<OtlpLogRecord>? LogRecords { get; init; }
}

public sealed record OtlpLogRecord
{
    public ulong TimeUnixNano { get; init; }
    public ulong ObservedTimeUnixNano { get; init; }
    public int? SeverityNumber { get; init; }
    public string? SeverityText { get; init; }
    public OtlpAnyValue? Body { get; init; }
    public List<OtlpKeyValue>? Attributes { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
}


public sealed record OtlpExportProfilesServiceRequest
{
    public List<OtlpResourceProfiles>? ResourceProfiles { get; init; }
}

public sealed record OtlpResourceProfiles
{
    public OtlpResource? Resource { get; init; }
    public List<OtlpScopeProfiles>? ScopeProfiles { get; init; }
    public string? SchemaUrl { get; init; }
}

public sealed record OtlpScopeProfiles
{
    public List<OtlpProfile>? Profiles { get; init; }
    public string? SchemaUrl { get; init; }
}

public sealed record OtlpProfile
{
    public string? ProfileId { get; init; }
    public OtlpValueType? SampleType { get; init; }
    public List<OtlpProfileSample>? Samples { get; init; }
    public ulong TimeUnixNano { get; init; }
    public ulong DurationNano { get; init; }
    public OtlpValueType? PeriodType { get; init; }
    public long? Period { get; init; }
    public string? OriginalPayloadFormat { get; init; }
    public string? OriginalPayload { get; init; }
    public List<OtlpKeyValue>? Attributes { get; init; }
    public int? DroppedAttributesCount { get; init; }

    public List<string>? StringTable { get; init; }
    public List<OtlpProfileFunction>? FunctionTable { get; init; }
    public List<OtlpProfileLocation>? LocationTable { get; init; }
    public List<OtlpProfileMapping>? MappingTable { get; init; }
    public List<OtlpProfileLink>? LinkTable { get; init; }
    public List<OtlpProfileStack>? StackTable { get; init; }
}

public sealed record OtlpValueType
{
    public int? TypeStrindex { get; init; }
    public int? UnitStrindex { get; init; }
}

public sealed record OtlpProfileSample
{
    public int? StackIndex { get; init; }
    public List<int>? AttributeIndices { get; init; }
    public int? LinkIndex { get; init; }
    public List<long>? Values { get; init; }
    public List<ulong>? TimestampsUnixNano { get; init; }
}

public sealed record OtlpProfileFunction
{
    public int? NameStrindex { get; init; }
    public int? SystemNameStrindex { get; init; }
    public int? FilenameStrindex { get; init; }
    public long? StartLine { get; init; }
}

public sealed record OtlpProfileLocation
{
    public int? MappingIndex { get; init; }
    public ulong? Address { get; init; }
    public List<OtlpProfileLine>? Lines { get; init; }
    public List<int>? AttributeIndices { get; init; }
}

public sealed record OtlpProfileLine
{
    public int? FunctionIndex { get; init; }
    public long? Line { get; init; }
    public long? Column { get; init; }
}

public sealed record OtlpProfileMapping
{
    public ulong? MemoryStart { get; init; }
    public ulong? MemoryLimit { get; init; }
    public ulong? FileOffset { get; init; }
    public int? FilenameStrindex { get; init; }
    public List<int>? AttributeIndices { get; init; }
}

public sealed record OtlpProfileLink
{
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
}

public sealed record OtlpProfileStack
{
    public List<int>? LocationIndices { get; init; }
}
