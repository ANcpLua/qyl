using qyl.collector.SchemaControl;

namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with schema control operations.
/// </summary>
public sealed partial class DuckDbStore
{
    // ==========================================================================
    // Schema Promotion Operations
    // ==========================================================================

    /// <summary>
    ///     Inserts a new schema promotion record.
    /// </summary>
    public async Task InsertSchemaPromotionAsync(
        SchemaPromotionRecord record,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO schema_promotions
                                  (id, profile_id, source_attribute, target_column, target_type, target_table, status, applied_at, created_at)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
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
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a schema promotion by its ID.
    /// </summary>
    public async Task<SchemaPromotionRecord?> GetSchemaPromotionAsync(
        string promotionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, profile_id, source_attribute, target_column, target_type,
                                 target_table, status, applied_at, created_at
                          FROM schema_promotions
                          WHERE id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = promotionId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return MapSchemaPromotion(reader);

        return null;
    }

    /// <summary>
    ///     Gets all schema promotions matching the given status.
    /// </summary>
    public async Task<IReadOnlyList<SchemaPromotionRecord>> GetSchemaPromotionsByStatusAsync(
        string status,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, profile_id, source_attribute, target_column, target_type,
                                 target_table, status, applied_at, created_at
                          FROM schema_promotions
                          WHERE status = $1
                          ORDER BY created_at DESC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = status });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<SchemaPromotionRecord>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapSchemaPromotion(reader));

        return results;
    }

    /// <summary>
    ///     Updates the status of a schema promotion.
    /// </summary>
    public async Task UpdateSchemaPromotionStatusAsync(
        string promotionId,
        string status,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
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
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Executes a raw DDL statement for schema promotion.
    /// </summary>
    public async Task ExecuteSchemaDdlAsync(
        string ddl,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = ddl;
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    // ==========================================================================
    // Private Methods - Schema Control Mapping
    // ==========================================================================

    private static SchemaPromotionRecord MapSchemaPromotion(IDataReader reader) =>
        new(
            reader.GetString(0),
            reader.Col(1).AsString,
            reader.Col(2).AsString ?? string.Empty,
            TargetColumn: reader.Col(3).AsString,
            ColumnType: reader.Col(4).AsString,
            TargetTable: reader.Col(5).AsString ?? string.Empty,
            SqlStatements: string.Empty,
            Status: reader.GetString(6),
            AppliedAt: reader.Col(7).AsDateTime,
            CreatedAt: reader.Col(8).AsDateTime ?? TimeProvider.System.GetUtcNow().UtcDateTime);
}
