// =============================================================================
// qyl Session Query Service - Pure DuckDB, No In-Memory State
// Replaces SessionAggregator.cs entirely
// =============================================================================

using System.Data.Common;
using DuckDB.NET.Data;
using qyl.collector.Storage;

namespace qyl.collector.Query;

public sealed class SessionQueryService(DuckDBConnection connection)
{
    // =========================================================================
    // List Sessions (replaces GetSessions)
    // =========================================================================

    public async Task<IReadOnlyList<SessionSummary>> GetSessionsAsync(
        int limit = 100,
        int offset = 0,
        string? serviceName = null,
        DateTime? after = null,
        CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COALESCE(session_id, trace_id) AS session_id,
                              MIN(start_time) AS start_time,
                              MAX(end_time) AS last_activity,
                              COUNT(*) AS span_count,
                              COUNT(DISTINCT trace_id) AS trace_count,
                              SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                              COALESCE(SUM(tokens_in), 0) AS input_tokens,
                              COALESCE(SUM(tokens_out), 0) AS output_tokens,
                              COUNT(CASE WHEN provider_name IS NOT NULL THEN 1 END) AS genai_request_count,
                              COALESCE(SUM(cost_usd), 0) AS total_cost_usd,
                              LIST(DISTINCT request_model) FILTER (WHERE request_model IS NOT NULL) AS models
                          FROM spans
                          WHERE ($1::VARCHAR IS NULL OR session_id = $1)
                            AND ($2::TIMESTAMP IS NULL OR start_time >= $2)
                          GROUP BY COALESCE(session_id, trace_id)
                          ORDER BY MAX(end_time) DESC
                          LIMIT $3 OFFSET $4
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = serviceName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = after ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });
        cmd.Parameters.Add(new DuckDBParameter { Value = offset });

        return await ExecuteSessionQueryAsync(cmd, ct);
    }

    // =========================================================================
    // Get Single Session (replaces GetSession)
    // =========================================================================

    public async Task<SessionSummary?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COALESCE(session_id, trace_id) AS session_id,
                              MIN(start_time) AS start_time,
                              MAX(end_time) AS last_activity,
                              COUNT(*) AS span_count,
                              COUNT(DISTINCT trace_id) AS trace_count,
                              SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                              COALESCE(SUM(tokens_in), 0) AS input_tokens,
                              COALESCE(SUM(tokens_out), 0) AS output_tokens,
                              COUNT(CASE WHEN provider_name IS NOT NULL THEN 1 END) AS genai_request_count,
                              COALESCE(SUM(cost_usd), 0) AS total_cost_usd,
                              LIST(DISTINCT request_model) FILTER (WHERE request_model IS NOT NULL) AS models
                          FROM spans
                          WHERE session_id = $1 OR (session_id IS NULL AND trace_id = $1)
                          GROUP BY COALESCE(session_id, trace_id)
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        var results = await ExecuteSessionQueryAsync(cmd, ct);
        return results.Count > 0 ? results[0] : null;
    }

    // =========================================================================
    // Session Timeline (spans in a session, ordered)
    // =========================================================================

    public async Task<IReadOnlyList<SpanRecord>> GetSessionSpansAsync(
        string sessionId,
        int limit = 1000,
        CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT *
                          FROM spans
                          WHERE session_id = $1 OR (session_id IS NULL AND trace_id = $1)
                          ORDER BY start_time ASC
                          LIMIT $2
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var spans = new List<SpanRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
            spans.Add(MapSpan(reader));

        return spans;
    }

    // =========================================================================
    // GenAI Stats per Session (or global)
    // =========================================================================

    public async Task<SessionGenAiStats> GetGenAiStatsAsync(
        string? sessionId = null,
        DateTime? after = null,
        CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COUNT(*) AS request_count,
                              COALESCE(SUM(tokens_in), 0) AS input_tokens,
                              COALESCE(SUM(tokens_out), 0) AS output_tokens,
                              COALESCE(SUM(cost_usd), 0) AS total_cost,
                              AVG(eval_score) FILTER (WHERE eval_score IS NOT NULL) AS avg_score,
                              LIST(DISTINCT provider_name) FILTER (WHERE provider_name IS NOT NULL) AS providers,
                              LIST(DISTINCT request_model) FILTER (WHERE request_model IS NOT NULL) AS models
                          FROM spans
                          WHERE provider_name IS NOT NULL
                            AND ($1::VARCHAR IS NULL OR session_id = $1)
                            AND ($2::TIMESTAMP IS NULL OR start_time >= $2)
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = after ?? (object)DBNull.Value });

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
            return new SessionGenAiStats
            {
                RequestCount = reader.GetInt64(0),
                InputTokens = reader.GetInt64(1),
                OutputTokens = reader.GetInt64(2),
                TotalCostUsd = reader.GetDecimal(3),
                AverageEvalScore = reader.IsDBNull(4) ? null : (float)reader.GetDouble(4),
                Providers = ReadStringList(reader, 5),
                Models = ReadStringList(reader, 6)
            };

        return new SessionGenAiStats();
    }

    // =========================================================================
    // Top Models (replaces any model aggregation logic)
    // =========================================================================

    public async Task<IReadOnlyList<ModelUsage>> GetTopModelsAsync(
        int limit = 10,
        DateTime? after = null,
        CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              provider_name,
                              request_model,
                              COUNT(*) AS call_count,
                              COALESCE(SUM(tokens_in), 0) AS input_tokens,
                              COALESCE(SUM(tokens_out), 0) AS output_tokens,
                              COALESCE(SUM(cost_usd), 0) AS total_cost,
                              AVG(EXTRACT(EPOCH FROM (end_time - start_time)) * 1000) AS avg_latency_ms,
                              PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (end_time - start_time)) * 1000) AS p95_latency_ms,
                              SUM(CASE WHEN status_code = 2 THEN 1.0 ELSE 0.0 END) / COUNT(*) * 100 AS error_rate
                          FROM spans
                          WHERE provider_name IS NOT NULL
                            AND ($1::TIMESTAMP IS NULL OR start_time >= $1)
                          GROUP BY provider_name, request_model
                          ORDER BY call_count DESC
                          LIMIT $2
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = after ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var models = new List<ModelUsage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
            models.Add(new ModelUsage
            {
                Provider = reader.IsDBNull(0) ? null : reader.GetString(0),
                Model = reader.IsDBNull(1) ? null : reader.GetString(1),
                CallCount = reader.GetInt64(2),
                InputTokens = reader.GetInt64(3),
                OutputTokens = reader.GetInt64(4),
                TotalCostUsd = reader.GetDecimal(5),
                AvgLatencyMs = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                P95LatencyMs = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                ErrorRate = reader.IsDBNull(8) ? 0 : reader.GetDouble(8)
            });

        return models;
    }

    // =========================================================================
    // Error Summary
    // =========================================================================

    public async Task<ErrorSummary> GetErrorSummaryAsync(
        string? sessionId = null,
        DateTime? after = null,
        CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COUNT(*) AS total_spans,
                              SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                              SUM(CASE WHEN status_code = 2 THEN 1.0 ELSE 0.0 END) / COUNT(*) * 100 AS error_rate
                          FROM spans
                          WHERE ($1::VARCHAR IS NULL OR session_id = $1)
                            AND ($2::TIMESTAMP IS NULL OR start_time >= $2)
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = after ?? (object)DBNull.Value });

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
            return new ErrorSummary
            {
                TotalSpans = reader.GetInt64(0),
                ErrorCount = reader.GetInt64(1),
                ErrorRate = reader.IsDBNull(2) ? 0 : reader.GetDouble(2)
            };

        return new ErrorSummary();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static async Task<List<SessionSummary>> ExecuteSessionQueryAsync(
        DuckDBCommand cmd,
        CancellationToken ct)
    {
        var sessions = new List<SessionSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var startTime = reader.GetDateTime(1);
            var lastActivity = reader.GetDateTime(2);
            var spanCount = reader.GetInt64(3);
            var errorCount = reader.GetInt64(5);

            sessions.Add(new SessionSummary
            {
                SessionId = reader.GetString(0),
                StartTime = startTime,
                LastActivity = lastActivity,
                DurationMs = (lastActivity - startTime).TotalMilliseconds,
                SpanCount = (int)spanCount,
                TraceCount = (int)reader.GetInt64(4),
                ErrorCount = (int)errorCount,
                ErrorRate = spanCount > 0 ? (double)errorCount / spanCount : 0,
                InputTokens = (int)reader.GetInt64(6),
                OutputTokens = (int)reader.GetInt64(7),
                TotalTokens = (int)(reader.GetInt64(6) + reader.GetInt64(7)),
                GenAiRequestCount = (int)reader.GetInt64(8),
                TotalCostUsd = reader.GetDecimal(9),
                Models = ReadStringList(reader, 10)
            });
        }

        return sessions;
    }

    private static IReadOnlyList<string> ReadStringList(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return [];

        // DuckDB LIST type comes as object[]
        var value = reader.GetValue(ordinal);
        return value switch
        {
            string[] arr => arr,
            object[] arr => arr.Select(x => x?.ToString() ?? "").Where(s => s.Length > 0).ToArray(),
            _ => []
        };
    }

    private static SpanRecord MapSpan(DbDataReader reader)
    {
        return new SpanRecord
        {
            TraceId = reader.GetString(reader.GetOrdinal("trace_id")),
            SpanId = reader.GetString(reader.GetOrdinal("span_id")),
            ParentSpanId = reader.IsDBNull(reader.GetOrdinal("parent_span_id"))
                ? null
                : reader.GetString(reader.GetOrdinal("parent_span_id")),
            SessionId = reader.IsDBNull(reader.GetOrdinal("session_id"))
                ? null
                : reader.GetString(reader.GetOrdinal("session_id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Kind = reader.IsDBNull(reader.GetOrdinal("kind"))
                ? null
                : reader.GetString(reader.GetOrdinal("kind")),
            StartTime = reader.GetDateTime(reader.GetOrdinal("start_time")),
            EndTime = reader.GetDateTime(reader.GetOrdinal("end_time")),
            StatusCode = reader.IsDBNull(reader.GetOrdinal("status_code"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("status_code")),
            StatusMessage = reader.IsDBNull(reader.GetOrdinal("status_message"))
                ? null
                : reader.GetString(reader.GetOrdinal("status_message")),
            ProviderName = reader.IsDBNull(reader.GetOrdinal("provider_name"))
                ? null
                : reader.GetString(reader.GetOrdinal("provider_name")),
            RequestModel = reader.IsDBNull(reader.GetOrdinal("request_model"))
                ? null
                : reader.GetString(reader.GetOrdinal("request_model")),
            TokensIn = reader.IsDBNull(reader.GetOrdinal("tokens_in"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("tokens_in")),
            TokensOut = reader.IsDBNull(reader.GetOrdinal("tokens_out"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("tokens_out")),
            CostUsd = reader.IsDBNull(reader.GetOrdinal("cost_usd"))
                ? null
                : reader.GetDecimal(reader.GetOrdinal("cost_usd")),
            EvalScore = reader.IsDBNull(reader.GetOrdinal("eval_score"))
                ? null
                : (float)reader.GetDouble(reader.GetOrdinal("eval_score")),
            EvalReason = reader.IsDBNull(reader.GetOrdinal("eval_reason"))
                ? null
                : reader.GetString(reader.GetOrdinal("eval_reason")),
            Attributes = reader.IsDBNull(reader.GetOrdinal("attributes"))
                ? null
                : reader.GetString(reader.GetOrdinal("attributes")),
            Events = reader.IsDBNull(reader.GetOrdinal("events"))
                ? null
                : reader.GetString(reader.GetOrdinal("events"))
        };
    }
}

// =============================================================================
// DTOs - Same shape as before, but computed from SQL
// =============================================================================

public sealed record SessionSummary
{
    public required string SessionId { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime LastActivity { get; init; }
    public double DurationMs { get; init; }
    public int SpanCount { get; init; }
    public int TraceCount { get; init; }
    public int ErrorCount { get; init; }
    public double ErrorRate { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens { get; init; }
    public int GenAiRequestCount { get; init; }
    public decimal TotalCostUsd { get; init; }
    public IReadOnlyList<string> Models { get; init; } = [];
}

public sealed record SessionGenAiStats
{
    public long RequestCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long TotalTokens => InputTokens + OutputTokens;
    public decimal TotalCostUsd { get; init; }
    public float? AverageEvalScore { get; init; }
    public IReadOnlyList<string> Providers { get; init; } = [];
    public IReadOnlyList<string> Models { get; init; } = [];
}

public sealed record ModelUsage
{
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public long CallCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public decimal TotalCostUsd { get; init; }
    public double AvgLatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public double ErrorRate { get; init; }
}

public sealed record ErrorSummary
{
    public long TotalSpans { get; init; }
    public long ErrorCount { get; init; }
    public double ErrorRate { get; init; }
}
