// =============================================================================
// SessionQueryService - Pure DuckDB queries with SpanQueryBuilder
// =============================================================================

using qyl.collector.Core;

namespace qyl.collector.Query;

/// <summary>
///     Session query service using SpanQueryBuilder for type-safe query construction.
///     All aggregations computed in DuckDB - no in-memory state.
/// </summary>
public sealed class SessionQueryService(DuckDbStore store)
{
    // =========================================================================
    // List Sessions
    // =========================================================================

    public async Task<IReadOnlyList<SessionQueryRow>> GetSessionsAsync(
        int limit = 100,
        int offset = 0,
        string? sessionFilter = null,
        DateTime? after = null,
        CancellationToken ct = default)
    {
        // Using raw SQL for complex COALESCE + GROUP BY pattern
        // SpanQueryBuilder is better for simpler queries
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COALESCE(session_id, trace_id) AS session_id,
                              MIN(start_time_unix_nano) AS start_time,
                              MAX(end_time_unix_nano) AS last_activity,
                              COUNT(*) AS span_count,
                              COUNT(DISTINCT trace_id) AS trace_count,
                              SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                              COALESCE(SUM(gen_ai_input_tokens), 0) AS input_tokens,
                              COALESCE(SUM(gen_ai_output_tokens), 0) AS output_tokens,
                              COUNT(CASE WHEN gen_ai_provider_name IS NOT NULL THEN 1 END) AS genai_request_count,
                              COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost_usd,
                              LIST(DISTINCT gen_ai_request_model) FILTER (WHERE gen_ai_request_model IS NOT NULL) AS models
                          FROM spans
                          WHERE ($1::VARCHAR IS NULL OR session_id = $1)
                            AND ($2::UBIGINT IS NULL OR start_time_unix_nano >= $2)
                          GROUP BY COALESCE(session_id, trace_id)
                          ORDER BY MAX(end_time_unix_nano) DESC
                          LIMIT $3 OFFSET $4
                          """;

        AddParams(cmd, sessionFilter, after, limit, offset);
        return await ExecuteSessionQueryAsync(cmd, ct);
    }

    // =========================================================================
    // Get Single Session
    // =========================================================================

    public async Task<SessionQueryRow?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COALESCE(session_id, trace_id) AS session_id,
                              MIN(start_time_unix_nano) AS start_time,
                              MAX(end_time_unix_nano) AS last_activity,
                              COUNT(*) AS span_count,
                              COUNT(DISTINCT trace_id) AS trace_count,
                              SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                              COALESCE(SUM(gen_ai_input_tokens), 0) AS input_tokens,
                              COALESCE(SUM(gen_ai_output_tokens), 0) AS output_tokens,
                              COUNT(CASE WHEN gen_ai_provider_name IS NOT NULL THEN 1 END) AS genai_request_count,
                              COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost_usd,
                              LIST(DISTINCT gen_ai_request_model) FILTER (WHERE gen_ai_request_model IS NOT NULL) AS models
                          FROM spans
                          WHERE session_id = $1 OR (session_id IS NULL AND trace_id = $1)
                          GROUP BY COALESCE(session_id, trace_id)
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        var results = await ExecuteSessionQueryAsync(cmd, ct);
        return results.Count > 0 ? results[0] : null;
    }

    // =========================================================================
    // Session Spans - Using SpanQueryBuilder
    // =========================================================================

    public async Task<IReadOnlyList<SpanStorageRow>> GetSessionSpansAsync(
        string sessionId,
        int limit = 1000,
        CancellationToken ct = default)
    {
        var sql = SpanQueryBuilder.Create()
            .SelectAll()
            .WhereWithFallback(SpanColumn.SessionId, SpanColumn.TraceId, 1)
            .OrderBy(SpanColumn.StartTimeUnixNano)
            .LimitParam(2)
            .Build();

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var spans = new List<SpanStorageRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
            spans.Add(MapSpan(reader));

        return spans;
    }

    // =========================================================================
    // GenAI Stats - Using SpanQueryBuilder
    // =========================================================================

    public async Task<SessionGenAiStats> GetGenAiStatsAsync(
        string? sessionId = null,
        DateTime? after = null,
        CancellationToken ct = default)
    {
        var sql = SpanQueryBuilder.Create()
            .SelectCount("request_count")
            .SelectSum(SpanColumn.GenAiInputTokens, "input_tokens")
            .SelectSum(SpanColumn.GenAiOutputTokens, "output_tokens")
            .Select("COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost")
            .SelectDistinctList(SpanColumn.GenAiProviderName, "providers")
            .SelectDistinctList(SpanColumn.GenAiRequestModel, "models")
            .WhereNotNull(SpanColumn.GenAiProviderName)
            .WhereOptional(SpanColumn.SessionId, 1)
            .WhereRaw("($2::UBIGINT IS NULL OR start_time_unix_nano >= $2)")
            .Build();

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = sql;
        AddParams(cmd, sessionId, after);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
        {
            return new SessionGenAiStats
            {
                RequestCount = reader.Col(0).GetInt64(0),
                InputTokens = reader.Col(1).GetInt64(0),
                OutputTokens = reader.Col(2).GetInt64(0),
                TotalCostUsd = reader.Col(3).GetDouble(0),
                Providers = await ReadStringListAsync(reader, 4),
                Models = await ReadStringListAsync(reader, 5)
            };
        }

        return new SessionGenAiStats();
    }

    // =========================================================================
    // Top Models - Using SpanQueryBuilder
    // =========================================================================

    public async Task<IReadOnlyList<ModelUsage>> GetTopModelsAsync(
        int limit = 10,
        DateTime? after = null,
        CancellationToken ct = default)
    {
        var sql = SpanQueryBuilder.Create()
            .Select(SpanColumn.GenAiProviderName)
            .Select(SpanColumn.GenAiRequestModel)
            .SelectCount("call_count")
            .SelectSum(SpanColumn.GenAiInputTokens, "input_tokens")
            .SelectSum(SpanColumn.GenAiOutputTokens, "output_tokens")
            .Select("COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost")
            .Select("AVG(duration_ns / 1000000.0) AS avg_latency_ms")
            .SelectPercentile(SpanColumn.Column("duration_ns / 1000000.0"), 0.95, "p95_latency_ms")
            .Select("SUM(CASE WHEN status_code = 2 THEN 1.0 ELSE 0.0 END) / COUNT(*) * 100 AS error_rate")
            .WhereNotNull(SpanColumn.GenAiProviderName)
            .WhereRaw("($1::UBIGINT IS NULL OR start_time_unix_nano >= $1)")
            .GroupBy(SpanColumn.GenAiProviderName)
            .GroupBy(SpanColumn.GenAiRequestModel)
            .OrderByDesc("call_count")
            .LimitParam(2)
            .Build();

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = sql;
        // Convert DateTime to UnixNano (UBIGINT) - cast to ulong BEFORE multiplication to avoid signed overflow
        object afterUnixNano = DBNull.Value;
        if (after.HasValue)
        {
            var ticks = (after.Value.ToUniversalTime() - DateTime.UnixEpoch).Ticks;
            afterUnixNano = (ulong)ticks * 100UL;
        }

        cmd.Parameters.Add(new DuckDBParameter { Value = afterUnixNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var models = new List<ModelUsage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            models.Add(new ModelUsage
            {
                Provider = reader.Col(0).AsString,
                Model = reader.Col(1).AsString,
                CallCount = reader.Col(2).GetInt64(0),
                InputTokens = reader.Col(3).GetInt64(0),
                OutputTokens = reader.Col(4).GetInt64(0),
                TotalCostUsd = reader.Col(5).GetDouble(0),
                AvgLatencyMs = reader.Col(6).GetDouble(0),
                P95LatencyMs = reader.Col(7).GetDouble(0),
                ErrorRate = reader.Col(8).GetDouble(0)
            });
        }

        return models;
    }

    // =========================================================================
    // Error Summary - Using SpanQueryBuilder
    // =========================================================================

    public async Task<ErrorSummary> GetErrorSummaryAsync(
        string? sessionId = null,
        DateTime? after = null,
        CancellationToken ct = default)
    {
        var sql = SpanQueryBuilder.Create()
            .SelectCount("total_spans")
            .Select("SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count")
            .Select("SUM(CASE WHEN status_code = 2 THEN 1.0 ELSE 0.0 END) / COUNT(*) * 100 AS error_rate")
            .WhereOptional(SpanColumn.SessionId, 1)
            .WhereRaw("($2::UBIGINT IS NULL OR start_time_unix_nano >= $2)")
            .Build();

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = sql;
        AddParams(cmd, sessionId, after);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
        {
            return new ErrorSummary
            {
                TotalSpans = reader.Col(0).GetInt64(0),
                ErrorCount = reader.Col(1).GetInt64(0),
                ErrorRate = reader.Col(2).GetDouble(0)
            };
        }

        return new ErrorSummary();
    }

    // =========================================================================
    // Spans by Trace - Using SpanQueryBuilder
    // =========================================================================

    public async Task<IReadOnlyList<SpanStorageRow>> GetSpansByTraceAsync(
        string traceId,
        CancellationToken ct = default)
    {
        var sql = SpanQueryBuilder.Create()
            .SelectAll()
            .WhereEq(SpanColumn.TraceId, 1)
            .OrderBy(SpanColumn.StartTimeUnixNano)
            .Build();

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new DuckDBParameter { Value = traceId });

        var spans = new List<SpanStorageRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
            spans.Add(MapSpan(reader));

        return spans;
    }

    // =========================================================================
    // GenAI Spans Only - Using SpanQueryBuilder
    // =========================================================================

    public async Task<IReadOnlyList<SpanStorageRow>> GetGenAiSpansAsync(
        string? sessionId = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        var builder = SpanQueryBuilder.Create()
            .SelectAll()
            .WhereNotNull(SpanColumn.GenAiProviderName);

        if (sessionId is not null)
            builder = builder.WhereEq(SpanColumn.SessionId, 1);

        var sql = builder
            .OrderByDesc(SpanColumn.StartTimeUnixNano)
            .Limit(limit)
            .Build();

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = sql;

        if (sessionId is not null)
        {
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
        }

        var spans = new List<SpanStorageRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
            spans.Add(MapSpan(reader));

        return spans;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void AddParams(DuckDBCommand cmd, string? sessionId, DateTime? after)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId ?? (object)DBNull.Value });
        // Convert DateTime to UnixNano (UBIGINT) - cast to ulong BEFORE multiplication to avoid signed overflow
        object afterUnixNano = DBNull.Value;
        if (after.HasValue)
        {
            var ticks = (after.Value.ToUniversalTime() - DateTime.UnixEpoch).Ticks;
            afterUnixNano = (ulong)ticks * 100UL;
        }

        cmd.Parameters.Add(new DuckDBParameter { Value = afterUnixNano });
    }

    private static void AddParams(DuckDBCommand cmd, string? sessionId, DateTime? after, int limit, int offset)
    {
        AddParams(cmd, sessionId, after);
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });
        cmd.Parameters.Add(new DuckDBParameter { Value = offset });
    }

    private static async Task<List<SessionQueryRow>> ExecuteSessionQueryAsync(DbCommand cmd, CancellationToken ct)
    {
        var sessions = new List<SessionQueryRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            // UBIGINT timestamps converted to DateTime
            var startTimeNano = reader.Col(1).GetUInt64(0);
            var lastActivityNano = reader.Col(2).GetUInt64(0);
            var startTime = TimeConversions.UnixNanoToDateTime(startTimeNano);
            var lastActivity = TimeConversions.UnixNanoToDateTime(lastActivityNano);

            var spanCount = reader.Col(3).GetInt64(0);
            var errorCount = reader.Col(5).GetInt64(0);
            var inputTokens = reader.Col(6).GetInt64(0);
            var outputTokens = reader.Col(7).GetInt64(0);

            sessions.Add(new SessionQueryRow
            {
                SessionId = reader.GetString(0),
                StartTime = startTime,
                LastActivity = lastActivity,
                DurationMs = (lastActivityNano - startTimeNano) / 1_000_000.0,
                SpanCount = spanCount,
                TraceCount = reader.Col(4).GetInt64(0),
                ErrorCount = errorCount,
                ErrorRate = spanCount > 0 ? (double)errorCount / spanCount : 0,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                GenAiRequestCount = reader.Col(8).GetInt64(0),
                TotalCostUsd = reader.Col(9).GetDouble(0),
                Models = await ReadStringListAsync(reader, 10)
            });
        }

        return sessions;
    }

    private static async Task<IReadOnlyList<string>> ReadStringListAsync(DbDataReader reader, int ordinal)
    {
        if (await reader.IsDBNullAsync(ordinal))
            return [];

        var value = reader.GetValue(ordinal);
        // DuckDB.NET 1.4.3 returns List<string> for LIST columns
        return value switch
        {
            IReadOnlyList<string> list => list, // Covers string[], List<string>, etc.
            object[] arr => arr.Select(static x => x.ToString() ?? "").Where(static s => s.Length > 0).ToArray(),
            _ => []
        };
    }

    private static SpanStorageRow MapSpan(DbDataReader reader) => SpanRowMapper.MapByName(reader);
}

// =============================================================================
// DTOs
// =============================================================================

/// <summary>
///     Internal query result for session aggregation.
///     Not the same as Qyl.Models.SessionQueryRow (protocol type).
/// </summary>
public sealed record SessionQueryRow
{
    public required string SessionId { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime LastActivity { get; init; }
    public double DurationMs { get; init; }
    public long SpanCount { get; init; }
    public long TraceCount { get; init; }
    public long ErrorCount { get; init; }
    public double ErrorRate { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long TotalTokens { get; init; }
    public long GenAiRequestCount { get; init; }
    public double TotalCostUsd { get; init; }
    public IReadOnlyList<string> Models { get; init; } = [];
}

public sealed record SessionGenAiStats
{
    public long RequestCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long TotalTokens => InputTokens + OutputTokens;
    public double TotalCostUsd { get; init; }
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
    public double TotalCostUsd { get; init; }
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
