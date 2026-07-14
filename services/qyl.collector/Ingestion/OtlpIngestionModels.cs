namespace Qyl.Collector.Ingestion;

internal sealed record TraceIngestionBatch(IReadOnlyList<SpanIngestionRecord> Spans);

internal sealed record LogIngestionBatch(IReadOnlyList<LogIngestionRecord> Logs);

internal sealed record ProfileIngestionBatch(IReadOnlyList<ProfileIngestionRecord> Profiles);

internal sealed record SpanIngestionRecord
{
    public string? ProjectIdHint { get; init; }
    public required string SpanId { get; init; }
    public required string TraceId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string Name { get; init; }
    public int? Kind { get; init; }
    public required ulong StartTimeUnixNano { get; init; }
    public required ulong EndTimeUnixNano { get; init; }
    public int? StatusCode { get; init; }
    public required string ServiceName { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> Attributes { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> ResourceAttributes { get; init; }
    public string? SchemaUrl { get; init; }
    public string? StatusMessage { get; init; }
    public IReadOnlyList<SpanEventIngest> Events { get; init; } = [];
    public IReadOnlyList<SpanLinkIngest> Links { get; init; } = [];
}

internal sealed record SpanEventIngest(
    string Name,
    ulong TimeUnixNano,
    IReadOnlyDictionary<string, OtlpAttributeValue> Attributes);

internal sealed record SpanLinkIngest(
    string TraceId,
    string SpanId,
    IReadOnlyDictionary<string, OtlpAttributeValue> Attributes);

internal sealed record LogIngestionRecord
{
    public string? ProjectIdHint { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public required ulong TimeUnixNano { get; init; }
    public ulong? ObservedTimeUnixNano { get; init; }
    public required int SeverityNumber { get; init; }
    public string? SeverityText { get; init; }
    public string? BodyText { get; init; }
    public required string ServiceName { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> Attributes { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> ResourceAttributes { get; init; }
}

internal sealed record ProfileIngestionRecord
{
    public string? ProjectIdHint { get; init; }
    public required string ProfileId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? SessionId { get; init; }
    public required ulong TimeUnixNano { get; init; }
    public required ulong DurationNano { get; init; }
    public required int SampleCount { get; init; }
    public string? SampleType { get; init; }
    public string? SampleUnit { get; init; }
    public string? OriginalPayloadFormat { get; init; }
    public required string ServiceName { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> Attributes { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> ResourceAttributes { get; init; }
    public string? SchemaUrl { get; init; }
    public required IReadOnlyList<ProfileFunctionIngestionRecord> Functions { get; init; }
    public required IReadOnlyList<ProfileLocationIngestionRecord> Locations { get; init; }
    public required IReadOnlyList<ProfileMappingIngestionRecord> Mappings { get; init; }
    public required IReadOnlyList<ProfileSampleIngestionRecord> Samples { get; init; }
    public required IReadOnlyList<ProfileStackIngestionRecord> Stacks { get; init; }
}

internal sealed record ProfileFunctionIngestionRecord
{
    public required int Ordinal { get; init; }
    public string? Name { get; init; }
    public string? SystemName { get; init; }
    public string? Filename { get; init; }
    public long? StartLine { get; init; }
}

internal sealed record ProfileLocationIngestionRecord
{
    public required int Ordinal { get; init; }
    public int? MappingOrdinal { get; init; }
    public ulong? Address { get; init; }
    public IReadOnlyList<ProfileLocationLineJson>? Lines { get; init; }
}

internal readonly record struct ProfileLocationLineJson(
    [property: JsonPropertyName("functionOrdinal")] int FunctionOrdinal,
    [property: JsonPropertyName("line")] long Line,
    [property: JsonPropertyName("column")] long Column);

internal sealed record ProfileMappingIngestionRecord
{
    public required int Ordinal { get; init; }
    public string? Filename { get; init; }
    public ulong? MemoryStart { get; init; }
    public ulong? MemoryLimit { get; init; }
    public ulong? FileOffset { get; init; }
}

internal sealed record ProfileSampleIngestionRecord
{
    public required int Ordinal { get; init; }
    public int? StackOrdinal { get; init; }
    public string? LinkTraceId { get; init; }
    public string? LinkSpanId { get; init; }
    public long[]? Values { get; init; }
    public ulong[]? TimestampsUnixNano { get; init; }
}

internal sealed record ProfileStackIngestionRecord
{
    public required int Ordinal { get; init; }
    public int[]? LocationOrdinals { get; init; }
}
