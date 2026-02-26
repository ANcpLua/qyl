// =============================================================================
// DuckDbStore — span_clusters read/write operations.
// Called by EmbeddingClusterWorker to persist semantic cluster assignments.
// =============================================================================

namespace qyl.collector.Storage;

/// <summary>A span that has not yet been assigned to a semantic cluster.</summary>
public sealed record UnclusteredSpan(string SpanId, string SpanName, string? InputMessages);

/// <summary>A cluster assignment row for <c>span_clusters</c>.</summary>
public sealed record SpanClusterRow(
    string SpanId,
    int ClusterId,
    string ClusterLabel,
    double Distance,
    string ModelVersion,
    DateTime ComputedAt);

public sealed partial class DuckDbStore
{
    // ==========================================================================
    // Read
    // ==========================================================================

    /// <summary>
    ///     Returns up to <paramref name="limit" /> gen_ai spans that have
    ///     <c>gen_ai.input.messages</c> in their attributes but no entry
    ///     in <c>span_clusters</c> yet.
    /// </summary>
    public async Task<IReadOnlyList<UnclusteredSpan>> GetUnclusteredChatSpansAsync(
        int limit = 200,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT s.span_id,
                                 s.name,
                                 s.attributes_json->>'gen_ai.input.messages'
                          FROM spans s
                          LEFT JOIN span_clusters sc ON s.span_id = sc.span_id
                          WHERE s.attributes_json->>'gen_ai.operation.name' IS NOT NULL
                            AND s.attributes_json->>'gen_ai.input.messages' IS NOT NULL
                            AND sc.span_id IS NULL
                          ORDER BY s.start_time_unix_nano DESC
                          LIMIT $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var result = new List<UnclusteredSpan>(limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(new UnclusteredSpan(
                reader.GetString(0),
                reader.GetString(1),
                await reader.IsDBNullAsync(2, ct).ConfigureAwait(false) ? null : reader.GetString(2)));
        }

        return result;
    }

    // ==========================================================================
    // Write
    // ==========================================================================

    /// <summary>
    ///     Upserts cluster assignments into <c>span_clusters</c>.
    ///     Uses the single write connection (channel-buffered).
    /// </summary>
    public async Task UpsertSpanClustersAsync(
        IReadOnlyList<SpanClusterRow> rows,
        CancellationToken ct = default)
    {
        if (rows.Count is 0) return;
        ThrowIfDisposed();

        var job = new WriteJob<int>(async (con, token) =>
        {
            const string sql = """
                               INSERT INTO span_clusters
                                   (span_id, cluster_id, cluster_label, distance, model_version, computed_at)
                               VALUES ($1, $2, $3, $4, $5, $6)
                               ON CONFLICT (span_id) DO UPDATE SET
                                   cluster_id    = EXCLUDED.cluster_id,
                                   cluster_label = EXCLUDED.cluster_label,
                                   distance      = EXCLUDED.distance,
                                   model_version = EXCLUDED.model_version,
                                   computed_at   = EXCLUDED.computed_at
                               """;

            var inserted = 0;
            foreach (var row in rows)
            {
                await using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.Add(new DuckDBParameter { Value = row.SpanId });
                cmd.Parameters.Add(new DuckDBParameter { Value = row.ClusterId });
                cmd.Parameters.Add(new DuckDBParameter { Value = row.ClusterLabel });
                cmd.Parameters.Add(new DuckDBParameter { Value = row.Distance });
                cmd.Parameters.Add(new DuckDBParameter { Value = row.ModelVersion });
                cmd.Parameters.Add(new DuckDBParameter { Value = row.ComputedAt });
                inserted += await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            return inserted;
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }
}
