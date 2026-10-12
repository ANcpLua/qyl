namespace Qyl.Collector.Storage;

// Only trace identities are retained; exemplar attributes may contain customer content.
[DuckDbTable("metric_exemplars", Indexes = "ProjectId,TimeUnixNano", OnConflict = "ON CONFLICT DO NOTHING")]
internal sealed partial record MetricExemplarRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string SeriesId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required ulong TimeUnixNano { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 3)]
    public required string TraceId { get; init; }
}
