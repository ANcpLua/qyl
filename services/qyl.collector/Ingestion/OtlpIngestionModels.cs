namespace Qyl.Collector.Ingestion;

internal sealed record TraceIngestionBatch(IReadOnlyList<SpanIngestionRecord> Spans);

internal sealed record LogIngestionBatch(IReadOnlyList<LogIngestionRecord> Logs);

/// <summary>
/// The converted points plus the OTLP partial-success accounting for what was dropped.
/// A rejected shape must not fail the whole export: the good metrics in the same request
/// are still stored, and the sender learns exactly which instrument to remove.
/// </summary>
internal sealed record MetricIngestionBatch(
    IReadOnlyList<MetricPointIngestionRecord> Points,
    long RejectedDataPoints,
    string? RejectionMessage);

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

/// <summary>
/// One OTLP data point carrying the stream metadata it was nested under. Flattening here
/// keeps the converter a pure projection of the wire shape; the storage mapper folds the
/// repeated metadata back into one series row per distinct stream.
/// </summary>
internal sealed record MetricPointIngestionRecord
{
    public string? ProjectIdHint { get; init; }
    public required string MetricName { get; init; }
    public required MetricKind Kind { get; init; }
    public required MetricTemporality Temporality { get; init; }
    public required bool IsMonotonic { get; init; }
    public string? Unit { get; init; }
    public string? Description { get; init; }
    public required string ServiceName { get; init; }
    public string? SchemaUrl { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> Attributes { get; init; }
    public required IReadOnlyDictionary<string, OtlpAttributeValue> ResourceAttributes { get; init; }
    public IReadOnlyList<ResourceEntityRefIngestionRecord> ResourceEntityRefs { get; init; } = [];

    public required ulong TimeUnixNano { get; init; }
    public ulong? StartTimeUnixNano { get; init; }

    /// <summary>Gauge and sum value; NULL for histograms and for NO_RECORDED_VALUE points.</summary>
    public double? Value { get; init; }

    public ulong? Count { get; init; }
    public double? Sum { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }

    /// <summary>Ascending explicit upper bounds, without the implicit +infinity bound.</summary>
    public IReadOnlyList<double>? BucketBounds { get; init; }

    /// <summary>Bucket counts, always one longer than <see cref="BucketBounds" />.</summary>
    public IReadOnlyList<ulong>? BucketCounts { get; init; }
}
