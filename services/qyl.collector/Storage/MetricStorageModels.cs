namespace Qyl.Collector.Storage;

/// <summary>
/// The metric shapes qyl persists. OTLP's summary point is deliberately absent: it carries
/// pre-computed quantiles with no buckets, so it can be neither re-aggregated over a time
/// window nor merged across series, and no OpenTelemetry SDK emits it. Ingest rejects it
/// explicitly rather than storing a shape the read API could not answer a question about.
/// </summary>
internal enum MetricKind : byte
{
    Gauge = 1,
    Sum = 2,
    Histogram = 3,

    /// <summary>
    /// An OTLP exponential histogram. Its buckets are materialized into the same explicit
    /// lower/upper bound vector every <see cref="Histogram" /> point uses, so the query path
    /// has exactly one histogram shape; the kind is retained only to report provenance.
    /// </summary>
    ExponentialHistogram = 4
}

internal enum MetricTemporality : byte
{
    Unspecified = 0,
    Delta = 1,
    Cumulative = 2
}

/// <summary>
/// One row per distinct (metric, attribute set, resource) stream — the series index.
/// It answers "which metrics exist" and "which series carry attribute X" without touching
/// the point table, which is the pair of questions an agent asks before it asks for values.
/// </summary>
[DuckDbTable(
    "metric_series",
    // Series listing is always project-scoped and almost always metric-scoped; one composite
    // index serves both because the project prefix is usable on its own.
    Indexes = "ProjectId,MetricName",
    OnConflict = """
    ON CONFLICT (project_id, series_id) DO UPDATE SET
        metric_kind = EXCLUDED.metric_kind,
        temporality = EXCLUDED.temporality,
        is_monotonic = EXCLUDED.is_monotonic,
        unit = EXCLUDED.unit,
        description = EXCLUDED.description,
        schema_url = EXCLUDED.schema_url,
        attributes_json = EXCLUDED.attributes_json,
        resource_json = EXCLUDED.resource_json,
        resource_entity_refs_json = EXCLUDED.resource_entity_refs_json,
        first_seen_unix_nano = least(first_seen_unix_nano, EXCLUDED.first_seen_unix_nano),
        last_seen_unix_nano = greatest(last_seen_unix_nano, EXCLUDED.last_seen_unix_nano)
    """)]
internal sealed partial record MetricSeriesRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }

    /// <summary>
    /// Deterministic digest of metric name, persisted attributes, service name and resource
    /// projection. Stable across restarts and exporters, so re-ingesting the same stream
    /// updates one row instead of forking a new series.
    /// </summary>
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string SeriesId { get; init; }

    public required string MetricName { get; init; }
    public required byte MetricKind { get; init; }
    public required byte Temporality { get; init; }
    public required byte IsMonotonic { get; init; }

    public string? Unit { get; init; }
    public string? Description { get; init; }
    public string? ServiceName { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(256)")]
    public string? SchemaUrl { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public string? AttributesJson { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public string? ResourceJson { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public string? ResourceEntityRefsJson { get; init; }

    public required ulong FirstSeenUnixNano { get; init; }
    public required ulong LastSeenUnixNano { get; init; }
}

/// <summary>
/// One row per data point. Narrow on purpose: everything constant for a stream lives in
/// <see cref="MetricSeriesRow" />, so a range scan reads only what changes over time.
/// </summary>
[DuckDbTable(
    "metric_points",
    // The primary key already orders points by (project, series, time), which is the range
    // scan every value query performs. This one extra index serves the two queries that cross
    // series: the retention sweep and "what happened in this project between t0 and t1".
    Indexes = "ProjectId,TimeUnixNano",
    OnConflict = """
    ON CONFLICT (project_id, series_id, time_unix_nano) DO UPDATE SET
        start_time_unix_nano = EXCLUDED.start_time_unix_nano,
        value = EXCLUDED.value,
        histogram_count = EXCLUDED.histogram_count,
        histogram_sum = EXCLUDED.histogram_sum,
        histogram_min = EXCLUDED.histogram_min,
        histogram_max = EXCLUDED.histogram_max,
        bucket_bounds_json = EXCLUDED.bucket_bounds_json,
        bucket_counts_json = EXCLUDED.bucket_counts_json
    """)]
internal sealed partial record MetricPointRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string SeriesId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required ulong TimeUnixNano { get; init; }

    public ulong? StartTimeUnixNano { get; init; }

    /// <summary>
    /// Gauge and sum value. NULL both for histogram points and for a point whose
    /// <c>NO_RECORDED_VALUE</c> flag was set — in either case there is no scalar to read,
    /// and a NULL is the one representation every aggregate already skips.
    /// </summary>
    public double? Value { get; init; }

    public ulong? HistogramCount { get; init; }
    public double? HistogramSum { get; init; }
    public double? HistogramMin { get; init; }
    public double? HistogramMax { get; init; }

    /// <summary>
    /// Ascending explicit upper bounds, JSON array of doubles. The implicit final bucket
    /// (+infinity) is not listed; <c>bucket_counts_json</c> is always one element longer.
    /// </summary>
    [DuckDbColumn(SqlType = "JSON")]
    public string? BucketBoundsJson { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public string? BucketCountsJson { get; init; }
}

/// <summary>A metric name and the shape shared by every series recorded under it.</summary>
internal sealed record MetricCatalogEntry(
    string MetricName,
    byte MetricKind,
    byte Temporality,
    byte IsMonotonic,
    string? Unit,
    string? Description,
    long SeriesCount,
    ulong LastSeenUnixNano);

/// <summary>One aggregated time bucket of a metric query.</summary>
internal sealed record MetricAggregatePoint(
    ulong BucketStartUnixNano,
    double? Value,
    ulong PointCount);

/// <summary>
/// One result stream of a metric query: the grouping key that produced it plus its buckets.
/// </summary>
internal sealed record MetricQuerySeries(
    string SeriesId,
    string? AttributesJson,
    IReadOnlyList<MetricAggregatePoint> Points);

internal readonly record struct MetricAttributeMatcher(string Key, string Value, bool IsPrefix);

internal enum MetricAggregation : byte
{
    Avg,
    Min,
    Max,
    Sum,
    Count,
    Last,
    P50,
    P90,
    P95,
    P99
}

internal readonly record struct MetricRetentionResult(int Points, int Series);
