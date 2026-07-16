using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore
{
    public Task ReplaceProviderCostBucketsAsync(
        string projectId,
        string provider,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        IReadOnlyList<ProviderCostBucketRow> buckets,
        ProviderCostSyncRow sync,
        CancellationToken ct = default) =>
        ExecuteWriteAsync(async (con, wct) =>
        {
            await using var tx = await con.BeginTransactionAsync(wct).ConfigureAwait(false);

            await using (var delete = con.CreateCommand())
            {
                delete.Transaction = tx;
                delete.CommandText = """
                                     DELETE FROM provider_cost_buckets
                                     WHERE project_id = $1
                                       AND provider = $2
                                       AND period_start >= $3
                                       AND period_start < $4
                                     """;
                delete.Parameters.Add(new DuckDBParameter { Value = projectId });
                delete.Parameters.Add(new DuckDBParameter { Value = provider });
                delete.Parameters.Add(new DuckDBParameter { Value = periodStart });
                delete.Parameters.Add(new DuckDBParameter { Value = periodEnd });
                await delete.ExecuteNonQueryAsync(wct).ConfigureAwait(false);
            }

            if (buckets.Count > 0)
            {
                await InsertRowsBatchedAsync(
                        con,
                        tx,
                        buckets,
                        ProviderCostBucketRow.AddParameters,
                        ProviderCostBucketRow.BuildMultiRowInsertSql,
                        buckets.Count,
                        wct)
                    .ConfigureAwait(false);
            }

            await InsertRowsBatchedAsync(
                    con,
                    tx,
                    [sync],
                    ProviderCostSyncRow.AddParameters,
                    ProviderCostSyncRow.BuildMultiRowInsertSql,
                    1,
                    wct)
                .ConfigureAwait(false);

            await tx.CommitAsync(wct).ConfigureAwait(false);
        }, ct);

    public Task UpsertProviderCostSyncAsync(ProviderCostSyncRow sync, CancellationToken ct = default) =>
        ExecuteWriteAsync(async (con, wct) =>
        {
            await using var tx = await con.BeginTransactionAsync(wct).ConfigureAwait(false);
            await InsertRowsBatchedAsync(
                    con,
                    tx,
                    [sync],
                    ProviderCostSyncRow.AddParameters,
                    ProviderCostSyncRow.BuildMultiRowInsertSql,
                    1,
                    wct)
                .ConfigureAwait(false);
            await tx.CommitAsync(wct).ConfigureAwait(false);
        }, ct);

    private static IReadOnlyList<ProviderCostBucketRow> ReadProviderCostBuckets(
        DuckDBConnection con,
        DbTransaction? transaction,
        string projectId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        var rows = new List<ProviderCostBucketRow>();
        using var cmd = con.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
                           SELECT {ProviderCostBucketRow.SelectColumnList}
                           FROM provider_cost_buckets
                           WHERE project_id = $1
                             AND period_start < $3
                             AND period_end > $2
                           ORDER BY period_start, provider, model_key
                           """;
        cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
        cmd.Parameters.Add(new DuckDBParameter { Value = periodStart });
        cmd.Parameters.Add(new DuckDBParameter { Value = periodEnd });
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(ProviderCostBucketRow.MapFromReader(reader));
        return rows;
    }

    private static IReadOnlyList<ProviderCostSyncRow> ReadProviderCostSync(
        DuckDBConnection con,
        DbTransaction? transaction,
        string projectId)
    {
        var rows = new List<ProviderCostSyncRow>();
        using var cmd = con.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
                           SELECT {ProviderCostSyncRow.SelectColumnList}
                           FROM provider_cost_sync
                           WHERE project_id = $1
                           ORDER BY provider
                           """;
        cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(ProviderCostSyncRow.MapFromReader(reader));
        return rows;
    }
}
