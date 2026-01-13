// =============================================================================
// SessionQueryService - Pure DuckDB queries with SpanQueryBuilder
// =============================================================================

namespace qyl.collector.Query;

/// <summary>
///     Session query service using SpanQueryBuilder for type-safe query construction.
///     All aggregations computed in DuckDB - no in-memory state.
/// </summary>
public sealed class SessionQueryService(DuckDBConnection connection)
{
    private readonly DuckDBConnection _connection = connection;
    // =========================================================================
    // List Sessions
    // =========================================================================

    public async Task<IReadOnlyList<SessionSummary>> GetSessionsAsync(
        int limit = 100,
        int offset = 0,
        string? sessionFilter = null,
        DateTime? after = null,
        CancellationToken ct = default)
    {
        // Using raw SQL for complex COALESCE + GROUP BY pattern
        // SpanQueryBuilder is better for simpler queries
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COALESCE(session_id, trace_id) AS session_id,
                              MIN(start_time) AS start_time,
                              MAX(end_time) AS last_activity,
                              COUNT(*) AS span_count,
                              COUNT(DISTINCT trace_id) AS trace_count,
                              SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                              COALESCE(SUM(genai_input_tokens), 0) AS input_tokens,
                              COALESCE(SUM(genai_output_tokens), 0) AS output_tokens,
                              COUNT(CASE WHEN genai_provider IS NOT NULL THEN 1 END) AS genai_request_count,
                              COALESCE(SUM(cost_usd), 0) AS total_cost_usd,
                              LIST(DISTINCT genai_request_model) FILTER (WHERE genai_request_model IS NOT NULL) AS models
                          FROM spans
                          WHERE ($1::VARCHAR IS NULL OR session_id = $1)
                            AND ($2::TIMESTAMP IS NULL OR start_time >= $2)
                          GROUP BY COALESCE(session_id, trace_id)
                          ORDER BY MAX(end_time) DESC
                          LIMIT $3 OFFSET $4
                          """;

        AddParams(cmd, sessionFilter, after, limit, offset);
        return await ExecuteSessionQueryAsync(cmd, ct);
    }

    // =========================================================================
    // Get Single Session
    // =========================================================================

    public async Task<SessionSummary?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COALESCE(session_id, trace_id) AS session_id,
                              MIN(start_time) AS start_time,
                              MAX(end_time) AS last_activity,
                              COUNT(*) AS span_count,
                              COUNT(DISTINCT trace_id) AS trace_count,
                              SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                              COALESCE(SUM(genai_input_tokens), 0) AS input_tokens,
                              COALESCE(SUM(genai_output_tokens), 0) AS output_tokens,
                              COUNT(CASE WHEN genai_provider IS NOT NULL THEN 1 END) AS genai_request_count,
                              COALESCE(SUM(cost_usd), 0) AS total_cost_usd,
                              LIST(DISTINCT genai_request_model) FILTER (WHERE genai_request_model IS NOT NULL) AS models
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
            .OrderBy(SpanColumn.StartTime)
            .LimitParam(2)
            .Build();

        await using var cmd = _connection.CreateCommand();
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
            .Select("COALESCE(SUM(cost_usd), 0) AS total_cost")
            .Select("AVG(eval_score) FILTER (WHERE eval_score IS NOT NULL) AS avg_score")
            .SelectDistinctList(SpanColumn.GenAiProviderName, "providers")
            .SelectDistinctList(SpanColumn.GenAiRequestModel, "models")
            .WhereNotNull(SpanColumn.GenAiProviderName)
            .WhereOptional(SpanColumn.SessionId, 1)
            .WhereRaw("($2::TIMESTAMP IS NULL OR start_time >= $2)")
            .Build();

        await using var cmd = _connection.CreateCommand();
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
                TotalCostUsd = reader.Col(3).GetDecimal(0),
                AverageEvalScore = reader.Col(4).AsFloat,
                Providers = await ReadStringListAsync(reader, 5),
                Models = await ReadStringListAsync(reader, 6)
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
            .Select("COALESCE(SUM(cost_usd), 0) AS total_cost")
            .Select("AVG(EXTRACT(EPOCH FROM (end_time - start_time)) * 1000) AS avg_latency_ms")
            .SelectPercentile(SpanColumn.Column("EXTRACT(EPOCH FROM (end_time - start_time)) * 1000"), 0.95,
                "p95_latency_ms")
            .Select("SUM(CASE WHEN status_code = 2 THEN 1.0 ELSE 0.0 END) / COUNT(*) * 100 AS error_rate")
            .WhereNotNull(SpanColumn.GenAiProviderName)
            .WhereRaw("($1::TIMESTAMP IS NULL OR start_time >= $1)")
            .GroupBy(SpanColumn.GenAiProviderName)
            .GroupBy(SpanColumn.GenAiRequestModel)
            .OrderByDesc("call_count")
            .LimitParam(2)
            .Build();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new DuckDBParameter { Value = after ?? (object)DBNull.Value });
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
                TotalCostUsd = reader.Col(5).GetDecimal(0),
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
            .WhereRaw("($2::TIMESTAMP IS NULL OR start_time >= $2)")
            .Build();

        await using var cmd = _connection.CreateCommand();
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
            .OrderBy(SpanColumn.StartTime)
            .Build();

        await using var cmd = _connection.CreateCommand();
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
            .OrderByDesc(SpanColumn.StartTime)
            .Limit(limit)
            .Build();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        if (sessionId is not null)
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

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
        cmd.Parameters.Add(new DuckDBParameter { Value = after ?? (object)DBNull.Value });
    }

    private static void AddParams(DuckDBCommand cmd, string? sessionId, DateTime? after, int limit, int offset)
    {
        AddParams(cmd, sessionId, after);
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });
        cmd.Parameters.Add(new DuckDBParameter { Value = offset });
    }

    private static async Task<List<SessionSummary>> ExecuteSessionQueryAsync(DuckDBCommand cmd, CancellationToken ct)
    {
        var sessions = new List<SessionSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var startTime = reader.Col(1).GetDateTime(default);
            var lastActivity = reader.Col(2).GetDateTime(default);
            var spanCount = reader.Col(3).GetInt64(0);
            var errorCount = reader.Col(5).GetInt64(0);
            var inputTokens = reader.Col(6).GetInt64(0);
            var outputTokens = reader.Col(7).GetInt64(0);

            sessions.Add(new SessionSummary
            {
                SessionId = reader.GetString(0),
                StartTime = startTime,
                LastActivity = lastActivity,
                DurationMs = (lastActivity - startTime).TotalMilliseconds,
                SpanCount = spanCount,
                TraceCount = reader.Col(4).GetInt64(0),
                ErrorCount = errorCount,
                ErrorRate = spanCount > 0 ? (double)errorCount / spanCount : 0,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                GenAiRequestCount = reader.Col(8).GetInt64(0),
                TotalCostUsd = reader.Col(9).GetDecimal(0),
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
            object[] arr => arr.Select(x => x?.ToString() ?? "").Where(s => s.Length > 0).ToArray(),
            _ => []
        };
    }

    private static SpanStorageRow MapSpan(DbDataReader reader) =>
        new()
        {
            TraceId = reader.GetString(reader.GetOrdinal("trace_id")),
            SpanId = reader.GetString(reader.GetOrdinal("span_id")),
            ParentSpanId = reader.Col("parent_span_id").AsString,
            Name = reader.GetString(reader.GetOrdinal("name")),
            Kind = reader.Col("kind").AsString,
            StartTime = reader.Col("start_time").GetDateTime(default),
            EndTime = reader.Col("end_time").GetDateTime(default),
            StatusCode = reader.Col("status_code").AsInt32,
            StatusMessage = reader.Col("status_message").AsString,
            ServiceName = reader.Col("service_name").AsString,
            SessionId = reader.Col("session_id").AsString,
            ProviderName = reader.Col("genai_provider").AsString,
            RequestModel = reader.Col("genai_request_model").AsString,
            TokensIn = reader.Col("genai_input_tokens").AsInt64,
            TokensOut = reader.Col("genai_output_tokens").AsInt64,
            CostUsd = reader.Col("cost_usd").AsDecimal,
            EvalScore = reader.Col("eval_score").AsFloat,
            EvalReason = reader.Col("eval_reason").AsString,
            Attributes = reader.Col("attributes").AsString,
            Events = reader.Col("events").AsString
        };
}

// =============================================================================
// DTOs
// =============================================================================

public sealed record SessionSummary
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
