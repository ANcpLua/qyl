namespace Qyl.Collector.Ingestion;

internal sealed record TraceIngestionBatch(IReadOnlyList<SpanIngestionRecord> Spans);

internal sealed record LogIngestionBatch(IReadOnlyList<LogIngestionRecord> Logs);

internal sealed record ResourceEntityRefIngestionRecord(
    string? SchemaUrl,
    string Type,
    IReadOnlyList<string> IdKeys,
    IReadOnlyList<string> DescriptionKeys);

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
    public IReadOnlyList<ResourceEntityRefIngestionRecord> ResourceEntityRefs { get; init; } = [];
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
    public string? EventName { get; init; }
    public required ulong TimeUnixNano { get; init; }
    public ulong? ObservedTimeUnixNano { get; init; }
    public required int SeverityNumber { get; init; }
    public string? SeverityText { get; init; }
    public string? BodyText { get; init; }
    public required string ServiceName { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> Attributes { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> ResourceAttributes { get; init; }
    public IReadOnlyList<ResourceEntityRefIngestionRecord> ResourceEntityRefs { get; init; } = [];
}
