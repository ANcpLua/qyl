using Qyl.Collector.SchemaControl;

namespace Qyl.Collector.Storage;

public sealed partial class DuckDbStore
{

    public async Task InsertSchemaPromotionAsync(
        SchemaPromotionRecord record,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO schema_promotions
                                  (id, profile_id, source_attribute, target_column, target_type, target_table, status, applied_at, created_at, sql_statements)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = record.PromotionId });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.RequestedBy ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ChangeType });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.TargetColumn ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ColumnType ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.TargetTable });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Status });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.AppliedAt ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.CreatedAt });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.SqlStatements });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public Task<SchemaPromotionRecord?> GetSchemaPromotionAsync(
        string promotionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<SchemaPromotionRecord?>(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT id, profile_id, source_attribute, target_column, target_type,
                                     target_table, status, applied_at, created_at, sql_statements
                              FROM schema_promotions
                              WHERE id = $1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = promotionId });

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
                return MapSchemaPromotion(reader);

            return null;
        }, ct);
    }

    public Task<IReadOnlyList<SchemaPromotionRecord>> GetSchemaPromotionsByStatusAsync(
        string status,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<SchemaPromotionRecord>>(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT id, profile_id, source_attribute, target_column, target_type,
                                     target_table, status, applied_at, created_at, sql_statements
                              FROM schema_promotions
                              WHERE status = $1
                              ORDER BY created_at DESC
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = status });

            using var reader = cmd.ExecuteReader();
            var results = new List<SchemaPromotionRecord>();
            while (reader.Read())
                results.Add(MapSchemaPromotion(reader));

            return results;
        }, ct);
    }

    public async Task UpdateSchemaPromotionStatusAsync(
        string promotionId,
        string status,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = status == "applied"
                ? """
                  UPDATE schema_promotions
                  SET status = $1, applied_at = now()
                  WHERE id = $2
                  """
                : """
                  UPDATE schema_promotions
                  SET status = $1
                  WHERE id = $2
                  """;
            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            cmd.Parameters.Add(new DuckDBParameter { Value = promotionId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task ExecuteSchemaDdlAsync(
        string ddl,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = ddl;
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);


    private static SchemaPromotionRecord MapSchemaPromotion(DbDataReader reader) =>
        new(
            reader.GetString(0),
            reader.Col(1).AsString,
            reader.Col(2).AsString ?? string.Empty,
            TargetColumn: reader.Col(3).AsString,
            ColumnType: reader.Col(4).AsString,
            TargetTable: reader.Col(5).AsString ?? string.Empty,
            SqlStatements: reader.Col(9).AsString ?? string.Empty,
            Status: reader.GetString(6),
            AppliedAt: reader.Col(7).AsDateTime,
            CreatedAt: reader.Col(8).AsDateTime ?? TimeProvider.System.GetUtcNow().UtcDateTime);
}
