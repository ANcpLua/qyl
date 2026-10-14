namespace Qyl.Collector.Storage;

// Per-request MVCC snapshot with a bounded window and explicit incomplete-scan metadata.
internal sealed record AnalysisReadQuery
{
    public ulong? Start { get; init; }
    public ulong? End { get; init; }
    public string[] TraceIds { get; init; } = [];
    public string[] SessionIds { get; init; } = [];
    public bool IncludeTelemetry { get; init; } = true;
    public bool ErrorsOnly { get; init; }
    public bool IncludeWindowWithTargets { get; init; }
    public const int MaximumRows = 25000;
}
internal sealed record AnalysisSnapshot(
    IReadOnlyList<SpanStorageRow> Spans,
    IReadOnlyList<LogStorageRow> Logs,
    IReadOnlyList<MetricSeriesRow> Series,
    IReadOnlyList<MetricPointRow> Points,
    IReadOnlyList<AnnotationRow> Annotations,
    IReadOnlyList<AnalysisStateRow> State,
    IReadOnlyList<SavedQueryRow> SavedQueries,
    bool ScanComplete = true)
{
    public IReadOnlyList<MetricExemplarRow> Exemplars { get; init; } = [];
}

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
    public Task<AnalysisSnapshot> ReadAnalysisSnapshotAsync(string projectId, AnalysisReadQuery? query = null, CancellationToken ct = default) =>
        ExecuteReadAsync(con =>
        {
            using var tx = con.BeginTransaction();
            var complete = true;
            query ??= new AnalysisReadQuery();
            List<T> Read<T>(string table, string columns, Func<DbDataReader, T> map)
            {
                using var cmd = con.CreateCommand();
                cmd.Transaction = tx;
                var telemetry = table is "spans" or "logs" or "metric_series" or "metric_points" or "metric_exemplars";
                if (telemetry && !query.IncludeTelemetry) return [];
                var predicates = new List<string> { "project_id = $1" }; var values = new List<object> { projectId };
                void Add(string column, string comparison, object value) { values.Add(value); predicates.Add(column + comparison + "$" + values.Count); }
                var timeColumn = table == "spans" ? "start_time_unix_nano" : table == "metric_series" ? "last_seen_unix_nano" : "time_unix_nano";
                var targets = new List<string>();
                if (table is "spans" or "logs")
                {
                    foreach (var id in query.TraceIds) { values.Add(id); targets.Add("trace_id = $" + values.Count); }
                    foreach (var id in query.SessionIds) { values.Add(id); targets.Add("session_id = $" + values.Count); }
                    if (targets.Count > 0)
                    {
                        if (query.IncludeWindowWithTargets && query.Start is { } from && query.End is { } until)
                        {
                            values.Add((decimal)from); var fromParameter = "$" + values.Count;
                            values.Add((decimal)until); targets.Add("(" + timeColumn + " >= " + fromParameter + " AND " + timeColumn + " < $" + values.Count + ")");
                        }
                        predicates.Add("(" + string.Join(" OR ", targets) + ")");
                    }
                }
                if (telemetry && targets.Count is 0)
                {
                    if (query.Start is { } start) Add(timeColumn, " >= ", (decimal)start);
                    if (query.End is { } end && table != "metric_series") Add(timeColumn, " < ", (decimal)end);
                }
                if (query.ErrorsOnly && table == "spans") predicates.Add("status_code = 2");
                cmd.CommandTimeout = 50;
                cmd.CommandText = "SELECT " + columns + " FROM " + table + " WHERE " + string.Join(" AND ", predicates)
                    + (telemetry ? " ORDER BY " + timeColumn + " DESC" : "") + " LIMIT " + (AnalysisReadQuery.MaximumRows + 1);
                AddParameters(cmd, values.ToArray());
                ct.ThrowIfCancellationRequested();
                using var cancellation = ct.Register(cmd.Cancel);
                using var reader = cmd.ExecuteReader();
                var rows = new List<T>();
                while (reader.Read()) { ct.ThrowIfCancellationRequested(); rows.Add(map(reader)); }
                if (rows.Count > AnalysisReadQuery.MaximumRows) { complete = false; rows.RemoveAt(rows.Count - 1); }
                return rows;
            }
            var result = new AnalysisSnapshot(
                Read("spans", SpanStorageRow.SelectColumnList, SpanStorageRow.MapFromReader),
                Read("logs", LogStorageRow.SelectColumnList, LogStorageRow.MapFromReader),
                Read("metric_series", MetricSeriesRow.SelectColumnList, MetricSeriesRow.MapFromReader),
                Read("metric_points", MetricPointRow.SelectColumnList, MetricPointRow.MapFromReader),
                Read("annotations", AnnotationRow.SelectColumnList, AnnotationRow.MapFromReader),
                Read("analysis_state", AnalysisStateRow.SelectColumnList, AnalysisStateRow.MapFromReader),
                Read("saved_queries", SavedQueryRow.SelectColumnList, SavedQueryRow.MapFromReader))
            { Exemplars = Read("metric_exemplars", MetricExemplarRow.SelectColumnList, MetricExemplarRow.MapFromReader) };
            tx.Commit();
            return result with { ScanComplete = complete };
        }, ct);

    public Task<IReadOnlyList<AnalysisStateRow>> ReadAnalysisStateAsync(string projectId, IReadOnlyList<string> keys, CancellationToken ct = default) =>
        ExecuteReadAsync<IReadOnlyList<AnalysisStateRow>>(con =>
        {
            if (keys.Count is 0) return [];
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT " + AnalysisStateRow.SelectColumnList + " FROM analysis_state WHERE project_id = $1 AND state_key IN ("
                + string.Join(",", Enumerable.Range(2, keys.Count).Select(i => "$" + i)) + ")";
            AddParameters(cmd, new object[] { projectId }.Concat(keys.Cast<object>()).ToArray());
            using var reader = cmd.ExecuteReader(); var rows = new List<AnalysisStateRow>();
            while (reader.Read()) { ct.ThrowIfCancellationRequested(); rows.Add(AnalysisStateRow.MapFromReader(reader)); }
            return rows;
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
