using DuckDB.NET.Data;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore
{
    private const int MaximumGenAiEtlAuditUsageBuckets = 100_000;

    private const string GenAiEtlAuditCallsCte = """
        WITH gen_ai_calls AS (
            SELECT
                COALESCE(NULLIF(LOWER(TRIM(service_name)), ''), 'unknown') AS service_name,
                NULLIF(LOWER(TRIM(gen_ai_operation_name)), '') AS operation_name,
                NULLIF(LOWER(TRIM(gen_ai_output_type)), '') AS output_type,
                NULLIF(LOWER(TRIM(gen_ai_provider_name)), '') AS provider_name,
                COALESCE(
                    NULLIF(TRIM(gen_ai_response_model), ''),
                    NULLIF(TRIM(gen_ai_request_model), '')
                ) AS model_name,
                CASE
                    WHEN NULLIF(TRIM(gen_ai_response_model), '') IS NOT NULL THEN 'response_model'
                    WHEN NULLIF(TRIM(gen_ai_request_model), '') IS NOT NULL THEN 'request_model_fallback'
                    ELSE NULL
                END AS model_identity_basis,
                duration_ns,
                status_code,
                gen_ai_input_tokens,
                gen_ai_output_tokens,
                gen_ai_cache_read_input_tokens,
                gen_ai_cache_creation_input_tokens,
                gen_ai_reasoning_tokens
            FROM spans AS span
            WHERE span.project_id = $1
              AND span.start_time_unix_nano >= $2
              AND span.start_time_unix_nano < $3
              AND (
                  span.gen_ai_operation_name IS NOT NULL
                  OR span.gen_ai_provider_name IS NOT NULL
                  OR span.gen_ai_request_model IS NOT NULL
                  OR span.gen_ai_response_model IS NOT NULL
                  OR span.gen_ai_input_tokens IS NOT NULL
                  OR span.gen_ai_output_tokens IS NOT NULL
              )
              -- Agent roll-up spans repeat the usage of their child model calls. Exclude only a
              -- proven roll-up with an actual GenAI child; a standalone invoke_agent remains
              -- observable rather than being dropped by name.
              AND NOT (
                  LOWER(COALESCE(span.gen_ai_operation_name, '')) = 'invoke_agent'
                  AND EXISTS (
                      SELECT 1
                      FROM spans AS child
                      WHERE child.project_id = span.project_id
                        AND child.trace_id = span.trace_id
                        AND child.parent_span_id = span.span_id
                        AND (
                            child.gen_ai_operation_name IS NOT NULL
                            OR child.gen_ai_provider_name IS NOT NULL
                            OR child.gen_ai_request_model IS NOT NULL
                            OR child.gen_ai_response_model IS NOT NULL
                            OR child.gen_ai_input_tokens IS NOT NULL
                            OR child.gen_ai_output_tokens IS NOT NULL
                        )
                  )
              )
        )
        """;

    public Task<IReadOnlyList<GenAiEtlAuditStorageRow>> GetGenAiEtlAuditRowsAsync(
        string projectId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default) =>
        ExecuteReadAsync<IReadOnlyList<GenAiEtlAuditStorageRow>>(con =>
            ReadGenAiEtlAuditRows(con, transaction: null, projectId, periodStart, periodEnd), ct);

    public Task<GenAiEtlAuditStorageSnapshot> GetGenAiEtlAuditSnapshotAsync(
        string projectId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default) =>
        ExecuteReadAsync(con =>
        {
            using var transaction = con.BeginTransaction();
            var snapshot = new GenAiEtlAuditStorageSnapshot(
                ReadGenAiEtlAuditRows(con, transaction, projectId, periodStart, periodEnd),
                ReadGenAiEtlAuditUsageBuckets(con, transaction, projectId, periodStart, periodEnd),
                ReadProviderCostBuckets(con, transaction, projectId, periodStart, periodEnd),
                ReadProviderCostSync(con, transaction, projectId));
            transaction.Commit();
            return snapshot;
        }, ct);

    private static IReadOnlyList<GenAiEtlAuditStorageRow> ReadGenAiEtlAuditRows(
        DuckDBConnection con,
        DbTransaction? transaction,
        string projectId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        var rows = new List<GenAiEtlAuditStorageRow>();
        using var cmd = con.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = string.Concat(GenAiEtlAuditCallsCte, "\n", """
            SELECT
                service_name,
                operation_name,
                output_type,
                provider_name,
                model_name,
                model_identity_basis,
                COUNT(*) AS call_count,
                COALESCE(SUM(GREATEST(COALESCE(gen_ai_input_tokens, 0), 0)), 0) AS input_tokens,
                COALESCE(SUM(GREATEST(COALESCE(gen_ai_output_tokens, 0), 0)), 0) AS output_tokens,
                COALESCE(SUM(GREATEST(COALESCE(gen_ai_cache_read_input_tokens, 0), 0)), 0) AS cache_read_input_tokens,
                COALESCE(SUM(GREATEST(COALESCE(gen_ai_cache_creation_input_tokens, 0), 0)), 0) AS cache_creation_input_tokens,
                COALESCE(SUM(GREATEST(COALESCE(gen_ai_reasoning_tokens, 0), 0)), 0) AS reasoning_output_tokens,
                COUNT(*) FILTER (WHERE
                    operation_name IN ('chat', 'generate_content', 'text_completion', 'embeddings')
                    AND gen_ai_input_tokens IS NOT NULL
                    AND (operation_name = 'embeddings' OR gen_ai_output_tokens IS NOT NULL)
                    AND gen_ai_input_tokens >= 0
                    AND (gen_ai_output_tokens IS NULL OR gen_ai_output_tokens >= 0)
                    AND COALESCE(gen_ai_cache_read_input_tokens, 0) >= 0
                    AND COALESCE(gen_ai_cache_creation_input_tokens, 0) >= 0
                    AND COALESCE(gen_ai_reasoning_tokens, 0) >= 0
                    AND CAST(COALESCE(gen_ai_cache_read_input_tokens, 0) AS HUGEINT)
                        + CAST(COALESCE(gen_ai_cache_creation_input_tokens, 0) AS HUGEINT)
                        <= CAST(gen_ai_input_tokens AS HUGEINT)
                    AND (
                        gen_ai_reasoning_tokens IS NULL
                        OR gen_ai_output_tokens IS NOT NULL
                        AND CAST(gen_ai_reasoning_tokens AS HUGEINT) <= CAST(gen_ai_output_tokens AS HUGEINT)
                    )
                ) AS token_usage_call_count,
                SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                COALESCE(AVG(CAST(duration_ns AS DOUBLE)) / 1000000.0, 0) AS average_latency_ms,
                COALESCE(QUANTILE_CONT(CAST(duration_ns AS DOUBLE), 0.95) / 1000000.0, 0) AS p95_latency_ms
            FROM gen_ai_calls
            GROUP BY service_name, operation_name, output_type, provider_name, model_name, model_identity_basis
            ORDER BY call_count DESC, service_name, operation_name, output_type, provider_name, model_name,
                model_identity_basis
            """);
        cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
        AddUnixNanoParam(cmd, periodStart);
        AddUnixNanoParam(cmd, periodEnd);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new GenAiEtlAuditStorageRow
            {
                ServiceName = DuckDbValueReader.ReadString(reader, 0, "unknown"),
                OperationName = DuckDbValueReader.ReadString(reader, 1),
                OutputType = DuckDbValueReader.ReadString(reader, 2),
                ProviderName = DuckDbValueReader.ReadString(reader, 3),
                ModelName = DuckDbValueReader.ReadString(reader, 4),
                ModelIdentityBasis = DuckDbValueReader.ReadString(reader, 5),
                CallCount = DuckDbValueReader.ReadInt64(reader, 6, 0),
                InputTokens = DuckDbValueReader.ReadInt64(reader, 7, 0),
                OutputTokens = DuckDbValueReader.ReadInt64(reader, 8, 0),
                CacheReadInputTokens = DuckDbValueReader.ReadInt64(reader, 9, 0),
                CacheCreationInputTokens = DuckDbValueReader.ReadInt64(reader, 10, 0),
                ReasoningOutputTokens = DuckDbValueReader.ReadInt64(reader, 11, 0),
                TokenUsageCallCount = DuckDbValueReader.ReadInt64(reader, 12, 0),
                ErrorCount = DuckDbValueReader.ReadInt64(reader, 13, 0),
                AverageLatencyMs = DuckDbValueReader.ReadDouble(reader, 14, 0),
                P95LatencyMs = DuckDbValueReader.ReadDouble(reader, 15, 0)
            });
        }

        return rows;
    }

    private static IReadOnlyList<GenAiEtlAuditUsageBucket> ReadGenAiEtlAuditUsageBuckets(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        var buckets = new List<GenAiEtlAuditUsageBucket>();
        using var cmd = con.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = string.Concat(GenAiEtlAuditCallsCte, "\n", """
            SELECT
                service_name,
                operation_name,
                output_type,
                provider_name,
                model_name,
                model_identity_basis,
                gen_ai_input_tokens,
                gen_ai_output_tokens,
                gen_ai_cache_read_input_tokens,
                gen_ai_cache_creation_input_tokens,
                gen_ai_reasoning_tokens,
                COUNT(*) AS call_count
            FROM gen_ai_calls
            GROUP BY
                service_name,
                operation_name,
                output_type,
                provider_name,
                model_name,
                model_identity_basis,
                gen_ai_input_tokens,
                gen_ai_output_tokens,
                gen_ai_cache_read_input_tokens,
                gen_ai_cache_creation_input_tokens,
                gen_ai_reasoning_tokens
            ORDER BY
                service_name,
                operation_name NULLS FIRST,
                output_type NULLS FIRST,
                provider_name NULLS FIRST,
                model_name NULLS FIRST,
                model_identity_basis NULLS FIRST,
                gen_ai_input_tokens NULLS FIRST,
                gen_ai_output_tokens NULLS FIRST,
                gen_ai_cache_read_input_tokens NULLS FIRST,
                gen_ai_cache_creation_input_tokens NULLS FIRST,
                gen_ai_reasoning_tokens NULLS FIRST
            """, "\nLIMIT ", (MaximumGenAiEtlAuditUsageBuckets + 1).ToString(CultureInfo.InvariantCulture));
        cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
        AddUnixNanoParam(cmd, periodStart);
        AddUnixNanoParam(cmd, periodEnd);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            buckets.Add(new GenAiEtlAuditUsageBucket
            {
                ServiceName = DuckDbValueReader.ReadString(reader, 0, "unknown"),
                OperationName = DuckDbValueReader.ReadString(reader, 1),
                OutputType = DuckDbValueReader.ReadString(reader, 2),
                ProviderName = DuckDbValueReader.ReadString(reader, 3),
                ModelName = DuckDbValueReader.ReadString(reader, 4),
                ModelIdentityBasis = DuckDbValueReader.ReadString(reader, 5),
                InputTokens = DuckDbValueReader.ReadInt64(reader, 6),
                OutputTokens = DuckDbValueReader.ReadInt64(reader, 7),
                CacheReadInputTokens = DuckDbValueReader.ReadInt64(reader, 8),
                CacheCreationInputTokens = DuckDbValueReader.ReadInt64(reader, 9),
                ReasoningOutputTokens = DuckDbValueReader.ReadInt64(reader, 10),
                CallCount = DuckDbValueReader.ReadInt64(reader, 11, 0)
            });
        }

        if (buckets.Count > MaximumGenAiEtlAuditUsageBuckets)
            buckets.RemoveAt(buckets.Count - 1);

        return buckets;
    }
}
