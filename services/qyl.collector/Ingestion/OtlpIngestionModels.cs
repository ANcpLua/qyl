namespace Qyl.Collector.Ingestion;

internal sealed record TraceIngestionBatch(IReadOnlyList<SpanIngestionRecord> Spans);

internal sealed record LogIngestionBatch(IReadOnlyList<LogIngestionRecord> Logs);

internal sealed record ProfileIngestionBatch(IReadOnlyList<ProfileIngestionRecord> Profiles);

// Storage discriminator for the OTLP metric data kind of a persisted data point.
internal static class MetricStorageTypes
{
    public const int Gauge = 1;
    public const int Sum = 2;
    public const int Histogram = 3;
    public const int ExponentialHistogram = 4;
    public const int Summary = 5;
}

internal sealed record MetricIngestionBatch(IReadOnlyList<MetricIngestionRecord> Metrics);

// One record per OTLP metric data point. Gauge/Sum points carry Value; Histogram and
// ExponentialHistogram points carry Count/Sum/Min/Max (bucket layout is preserved as JSON).
internal sealed record MetricIngestionRecord
{
    public string? ProjectIdHint { get; init; }
    public required string MetricName { get; init; }
    public required int MetricType { get; init; }
    public string? Unit { get; init; }
    public string? Description { get; init; }
    public string? ScopeName { get; init; }
    public required ulong TimeUnixNano { get; init; }
    public ulong? StartTimeUnixNano { get; init; }
    public double? Value { get; init; }
    public ulong? Count { get; init; }
    public double? Sum { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public string? BucketsJson { get; init; }
    public bool? IsMonotonic { get; init; }
    public int? AggregationTemporality { get; init; }
    public required string ServiceName { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> Attributes { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> ResourceAttributes { get; init; }
}

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
