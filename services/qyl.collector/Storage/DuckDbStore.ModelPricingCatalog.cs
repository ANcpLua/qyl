using DuckDB.NET.Data;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore
{
    public Task<bool> ActivateModelPricingCatalogSnapshotAsync(
        ModelPricingCatalogSourceRow source,
        ModelPricingCatalogSnapshotRow snapshot,
        IReadOnlyList<ModelPricingCatalogModelRow> models,
        IReadOnlyList<ModelPricingCatalogOverrideRow> overrides,
        IReadOnlyList<ModelPricingCatalogRateRow> rates,
        int retainedSnapshots,
        CancellationToken ct = default) =>
        ExecuteWriteAsync(async (con, wct) =>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(retainedSnapshots, 1);
            await using var tx = await con.BeginTransactionAsync(wct).ConfigureAwait(false);
            string? previousSnapshotId = null;
            string? previousConfigurationFingerprint = null;
            await using (var current = con.CreateCommand())
            {
                current.Transaction = tx;
                current.CommandText = $"""
                                       SELECT {ModelPricingCatalogSourceRow.SelectColumnList}
                                       FROM {ModelPricingCatalogSourceRow.TableName}
                                       WHERE source_id = $1
                                       """;
                current.Parameters.Add(new DuckDBParameter { Value = source.SourceId });
                await using var reader = await current.ExecuteReaderAsync(wct).ConfigureAwait(false);
                if (await reader.ReadAsync(wct).ConfigureAwait(false))
                {
                    var row = ModelPricingCatalogSourceRow.MapFromReader(reader);
                    previousSnapshotId = row.ActiveSnapshotId;
                    previousConfigurationFingerprint = row.ActiveConfigurationFingerprint;
                }
            }

            var snapshotExists = false;
            await using (var existing = con.CreateCommand())
            {
                existing.Transaction = tx;
                existing.CommandText = """
                                       SELECT 1
                                       FROM model_pricing_catalog_snapshots
                                       WHERE source_id = $1 AND snapshot_id = $2
                                       """;
                existing.Parameters.Add(new DuckDBParameter { Value = snapshot.SourceId });
                existing.Parameters.Add(new DuckDBParameter { Value = snapshot.SnapshotId });
                snapshotExists = await existing.ExecuteScalarAsync(wct).ConfigureAwait(false) is not null;
            }

            if (!snapshotExists)
            {
                await InsertRowsBatchedAsync(
                        con, tx, [snapshot], ModelPricingCatalogSnapshotRow.AddParameters,
                        ModelPricingCatalogSnapshotRow.BuildMultiRowInsertSql, 1, wct)
                    .ConfigureAwait(false);
                await InsertRowsBatchedAsync(
                        con, tx, models, ModelPricingCatalogModelRow.AddParameters,
                        ModelPricingCatalogModelRow.BuildMultiRowInsertSql, 200, wct)
                    .ConfigureAwait(false);
                await InsertRowsBatchedAsync(
                        con, tx, overrides, ModelPricingCatalogOverrideRow.AddParameters,
                        ModelPricingCatalogOverrideRow.BuildMultiRowInsertSql, 200, wct)
                    .ConfigureAwait(false);
                await InsertRowsBatchedAsync(
                        con, tx, rates, ModelPricingCatalogRateRow.AddParameters,
                        ModelPricingCatalogRateRow.BuildMultiRowInsertSql, 200, wct)
                    .ConfigureAwait(false);
            }

            await InsertRowsBatchedAsync(
                    con, tx, [source], ModelPricingCatalogSourceRow.AddParameters,
                    ModelPricingCatalogSourceRow.BuildMultiRowInsertSql, 1, wct)
                .ConfigureAwait(false);
            await PruneModelPricingCatalogSnapshotsAsync(
                    con,
                    tx,
                    source.SourceId,
                    snapshot.SnapshotId,
                    retainedSnapshots,
                    wct)
                .ConfigureAwait(false);
            await tx.CommitAsync(wct).ConfigureAwait(false);

            return !string.Equals(previousSnapshotId, snapshot.SnapshotId, StringComparison.Ordinal) ||
                   !string.Equals(
                       previousConfigurationFingerprint,
                       source.ConfiguredFingerprint,
                       StringComparison.Ordinal);
        }, ct);

    private static async Task PruneModelPricingCatalogSnapshotsAsync(
        DuckDBConnection connection,
        DbTransaction transaction,
        string sourceId,
        string activeSnapshotId,
        int retainedSnapshots,
        CancellationToken cancellationToken)
    {
        var obsolete = new List<string>();
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = $"""
                                  SELECT snapshot_id
                                  FROM {ModelPricingCatalogSnapshotRow.TableName}
                                  WHERE source_id = $1 AND snapshot_id <> $2
                                  ORDER BY retrieved_at DESC, snapshot_id DESC
                                  OFFSET $3
                                  """;
            select.Parameters.Add(new DuckDBParameter { Value = sourceId });
            select.Parameters.Add(new DuckDBParameter { Value = activeSnapshotId });
            select.Parameters.Add(new DuckDBParameter { Value = retainedSnapshots - 1 });
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (DuckDbValueReader.ReadString(reader, 0) is { } snapshotId)
                    obsolete.Add(snapshotId);
            }
        }

        string[] deleteStatements =
        [
            "DELETE FROM model_pricing_catalog_rates WHERE source_id = $1 AND snapshot_id = $2",
            "DELETE FROM model_pricing_catalog_overrides WHERE source_id = $1 AND snapshot_id = $2",
            "DELETE FROM model_pricing_catalog_models WHERE source_id = $1 AND snapshot_id = $2",
            "DELETE FROM model_pricing_catalog_snapshots WHERE source_id = $1 AND snapshot_id = $2"
        ];
        foreach (var snapshotId in obsolete)
        {
            foreach (var statement in deleteStatements)
            {
                await using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = statement;
                delete.Parameters.Add(new DuckDBParameter { Value = sourceId });
                delete.Parameters.Add(new DuckDBParameter { Value = snapshotId });
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task UpsertModelPricingCatalogSourceAsync(
        ModelPricingCatalogSourceRow source,
        CancellationToken ct = default) =>
        ExecuteWriteAsync(async (con, wct) =>
        {
            await using var tx = await con.BeginTransactionAsync(wct).ConfigureAwait(false);
            await InsertRowsBatchedAsync(
                    con, tx, [source], ModelPricingCatalogSourceRow.AddParameters,
                    ModelPricingCatalogSourceRow.BuildMultiRowInsertSql, 1, wct)
                .ConfigureAwait(false);
            await tx.CommitAsync(wct).ConfigureAwait(false);
        }, ct);

    public Task<ModelPricingCatalogStorageSnapshot?> GetModelPricingCatalogAsync(
        string sourceId,
        CancellationToken ct = default) =>
        ExecuteReadAsync<ModelPricingCatalogStorageSnapshot?>(con =>
        {
            using var tx = con.BeginTransaction();
            ModelPricingCatalogSourceRow? source = null;
            using (var sourceCommand = con.CreateCommand())
            {
                sourceCommand.Transaction = tx;
                sourceCommand.CommandText = $"""
                                             SELECT {ModelPricingCatalogSourceRow.SelectColumnList}
                                             FROM model_pricing_catalog_sources
                                             WHERE source_id = $1
                                             """;
                sourceCommand.Parameters.Add(new DuckDBParameter { Value = sourceId });
                using var reader = sourceCommand.ExecuteReader();
                if (reader.Read()) source = ModelPricingCatalogSourceRow.MapFromReader(reader);
            }

            if (source?.ActiveSnapshotId is not { } snapshotId ||
                !string.Equals(
                    source.ActiveConfigurationFingerprint,
                    source.ConfiguredFingerprint,
                    StringComparison.Ordinal))
            {
                tx.Commit();
                return null;
            }

            ModelPricingCatalogSnapshotRow? snapshot = null;
            using (var snapshotCommand = con.CreateCommand())
            {
                snapshotCommand.Transaction = tx;
                snapshotCommand.CommandText = $"""
                                               SELECT {ModelPricingCatalogSnapshotRow.SelectColumnList}
                                               FROM model_pricing_catalog_snapshots
                                               WHERE source_id = $1 AND snapshot_id = $2
                                               """;
                snapshotCommand.Parameters.Add(new DuckDBParameter { Value = sourceId });
                snapshotCommand.Parameters.Add(new DuckDBParameter { Value = snapshotId });
                using var reader = snapshotCommand.ExecuteReader();
                if (reader.Read()) snapshot = ModelPricingCatalogSnapshotRow.MapFromReader(reader);
            }

            if (snapshot is null)
            {
                tx.Commit();
                return null;
            }

            var result = new ModelPricingCatalogStorageSnapshot(
                source,
                snapshot,
                ReadCatalogRows(
                    con,
                    tx,
                    $"SELECT {ModelPricingCatalogModelRow.SelectColumnList} FROM model_pricing_catalog_models WHERE source_id = $1 AND snapshot_id = $2 ORDER BY model_id",
                    sourceId,
                    snapshotId,
                    ModelPricingCatalogModelRow.MapFromReader),
                ReadCatalogRows(
                    con,
                    tx,
                    $"SELECT {ModelPricingCatalogOverrideRow.SelectColumnList} FROM model_pricing_catalog_overrides WHERE source_id = $1 AND snapshot_id = $2 ORDER BY model_id, priority",
                    sourceId,
                    snapshotId,
                    ModelPricingCatalogOverrideRow.MapFromReader),
                ReadCatalogRows(
                    con,
                    tx,
                    $"SELECT {ModelPricingCatalogRateRow.SelectColumnList} FROM model_pricing_catalog_rates WHERE source_id = $1 AND snapshot_id = $2 ORDER BY model_id, tier_priority, source_meter",
                    sourceId,
                    snapshotId,
                    ModelPricingCatalogRateRow.MapFromReader));
            tx.Commit();
            return result;
        }, ct);

    public Task<ModelPricingCatalogSourceState?> GetModelPricingCatalogSourceAsync(
        string sourceId,
        CancellationToken ct = default) =>
        ExecuteReadAsync<ModelPricingCatalogSourceState?>(con =>
        {
            using var tx = con.BeginTransaction();
            ModelPricingCatalogSourceRow? source = null;
            using (var sourceCommand = con.CreateCommand())
            {
                sourceCommand.Transaction = tx;
                sourceCommand.CommandText = $"""
                                             SELECT {ModelPricingCatalogSourceRow.SelectColumnList}
                                             FROM model_pricing_catalog_sources
                                             WHERE source_id = $1
                                             """;
                sourceCommand.Parameters.Add(new DuckDBParameter { Value = sourceId });
                using var reader = sourceCommand.ExecuteReader();
                if (reader.Read()) source = ModelPricingCatalogSourceRow.MapFromReader(reader);
            }

            if (source is null)
            {
                tx.Commit();
                return null;
            }

            ModelPricingCatalogSnapshotRow? snapshot = null;
            if (source.ActiveSnapshotId is { } snapshotId)
            {
                using var snapshotCommand = con.CreateCommand();
                snapshotCommand.Transaction = tx;
                snapshotCommand.CommandText = $"""
                                              SELECT {ModelPricingCatalogSnapshotRow.SelectColumnList}
                                              FROM model_pricing_catalog_snapshots
                                              WHERE source_id = $1 AND snapshot_id = $2
                                              """;
                snapshotCommand.Parameters.Add(new DuckDBParameter { Value = source.SourceId });
                snapshotCommand.Parameters.Add(new DuckDBParameter { Value = snapshotId });
                using var reader = snapshotCommand.ExecuteReader();
                if (reader.Read()) snapshot = ModelPricingCatalogSnapshotRow.MapFromReader(reader);
            }

            tx.Commit();
            return new ModelPricingCatalogSourceState(source, snapshot);
        }, ct);

    private static IReadOnlyList<T> ReadCatalogRows<T>(
        DuckDBConnection connection,
        DbTransaction transaction,
        string sql,
        string sourceId,
        string snapshotId,
        Func<DbDataReader, T> map)
    {
        var rows = new List<T>();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.Add(new DuckDBParameter { Value = sourceId });
        command.Parameters.Add(new DuckDBParameter { Value = snapshotId });
        using var reader = command.ExecuteReader();
        while (reader.Read()) rows.Add(map(reader));
        return rows;
    }
}
