using DuckDB.NET.Data;

namespace Qyl.Collector.Storage;

// Read all relevant persisted signals in one MVCC transaction. There is deliberately no row cap:
// callers apply predicates before paging and a cancellation deadline bounds expensive queries.
internal sealed record AnalysisSnapshot(
    IReadOnlyList<SpanStorageRow> Spans,
    IReadOnlyList<LogStorageRow> Logs,
    IReadOnlyList<MetricSeriesRow> Series,
    IReadOnlyList<MetricPointRow> Points,
    IReadOnlyList<AnnotationRow> Annotations,
    IReadOnlyList<AnalysisStateRow> State,
    IReadOnlyList<SavedQueryRow> SavedQueries);

[DuckDbTable("analysis_state", OnConflict = """
    ON CONFLICT (project_id, state_key) DO UPDATE SET
        value_json = EXCLUDED.value_json,
        etag = EXCLUDED.etag,
        updated_at_unix_nano = EXCLUDED.updated_at_unix_nano
    """)]
internal sealed partial record AnalysisStateRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string StateKey { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public required string ValueJson { get; init; }
    public required string Etag { get; init; }
    public required ulong UpdatedAtUnixNano { get; init; }
}

internal sealed partial class DuckDbStore
{
    public Task<AnalysisSnapshot> ReadAnalysisSnapshotAsync(string projectId, CancellationToken ct = default) =>
        ExecuteReadAsync(con =>
        {
            using var tx = con.BeginTransaction();
            List<T> Read<T>(string table, string columns, Func<DbDataReader, T> map)
            {
                using var cmd = con.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT " + columns + " FROM " + table + " WHERE project_id = $1";
                AddParameters(cmd, projectId);
                using var reader = cmd.ExecuteReader();
                var rows = new List<T>();
                while (reader.Read()) { ct.ThrowIfCancellationRequested(); rows.Add(map(reader)); }
                return rows;
            }
            var result = new AnalysisSnapshot(
                Read("spans", SpanStorageRow.SelectColumnList, SpanStorageRow.MapFromReader),
                Read("logs", LogStorageRow.SelectColumnList, LogStorageRow.MapFromReader),
                Read("metric_series", MetricSeriesRow.SelectColumnList, MetricSeriesRow.MapFromReader),
                Read("metric_points", MetricPointRow.SelectColumnList, MetricPointRow.MapFromReader),
                Read("annotations", AnnotationRow.SelectColumnList, AnnotationRow.MapFromReader),
                Read("analysis_state", AnalysisStateRow.SelectColumnList, AnalysisStateRow.MapFromReader),
                Read("saved_queries", SavedQueryRow.SelectColumnList, SavedQueryRow.MapFromReader));
            tx.Commit();
            return result;
        }, ct);

    public Task<ResourceWriteOutcome<AnalysisStateRow>> WriteAnalysisStateAsync(
        AnalysisStateRow row, string? expectedEtag, CancellationToken ct = default) =>
        ExecuteWriteAsync(async (con, token) =>
        {
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT " + AnalysisStateRow.SelectColumnList +
                " FROM analysis_state WHERE project_id = $1 AND state_key = $2";
            AddParameters(cmd, row.ProjectId, row.StateKey);
            AnalysisStateRow? current;
            await using (var reader = cmd.ExecuteReader())
                current = reader.Read() ? AnalysisStateRow.MapFromReader(reader) : null;
            if (current?.Etag != expectedEtag)
                return new ResourceWriteOutcome<AnalysisStateRow>(ResourceWriteStatus.PreconditionFailed,
                    CurrentEtag: current?.Etag);
            await InsertRowsBatchedAsync(con, tx, [row], AnalysisStateRow.AddParameters,
                AnalysisStateRow.BuildMultiRowInsertSql, 1, token).ConfigureAwait(false);
            await tx.CommitAsync(token).ConfigureAwait(false);
            return new ResourceWriteOutcome<AnalysisStateRow>(ResourceWriteStatus.Updated, row);
        }, ct);
}
