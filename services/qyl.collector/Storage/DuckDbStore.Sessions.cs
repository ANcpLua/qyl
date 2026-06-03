using DuckDB.NET.Data;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore
{
    private const int SessionFacetValueLimit = 16;

    private static string SessionSelectColumns => $$"""
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
                                                        MIN(DISTINCT gen_ai_provider_name, {{SessionFacetValueLimit}}) FILTER (WHERE gen_ai_provider_name IS NOT NULL) AS providers,
                                                        MIN(DISTINCT gen_ai_request_model, {{SessionFacetValueLimit}}) FILTER (WHERE gen_ai_request_model IS NOT NULL) AS models,
                                                        MIN(DISTINCT service_name, {{SessionFacetValueLimit}}) FILTER (WHERE service_name IS NOT NULL) AS services
                                                    FROM spans
                                                    """;

    public async Task<IReadOnlyList<SessionQueryRow>> GetSessionsAsync(
        int limit = 100,
        int offset = 0,
        bool? isActive = null,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default) =>
        await ExecuteReadAsync<IReadOnlyList<SessionQueryRow>>(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = SessionSelectColumns
                              + " WHERE ($1::UBIGINT IS NULL OR start_time_unix_nano >= $1)"
                              + " AND ($2::UBIGINT IS NULL OR start_time_unix_nano <= $2)"
                              + " GROUP BY COALESCE(session_id, trace_id)"
                              + " HAVING ($3::BOOLEAN IS NULL OR (MAX(end_time_unix_nano) >= $4) = $3)"
                              + " ORDER BY MAX(end_time_unix_nano) DESC"
                              + " LIMIT $5 OFFSET $6";

            AddUnixNanoParam(cmd, startTime);
            AddUnixNanoParam(cmd, endTime);
            cmd.Parameters.Add(new DuckDBParameter { Value = isActive ?? (object)DBNull.Value });
            AddUnixNanoParam(cmd, TimeProvider.System.GetUtcNow().AddMinutes(-30));
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });
            cmd.Parameters.Add(new DuckDBParameter { Value = offset });
            return ExecuteSessionQuery(cmd);
        }, ct).ConfigureAwait(false);

    public async Task<SessionQueryRow?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var results = await ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = SessionSelectColumns
                              + " WHERE session_id = $1 OR (session_id IS NULL AND trace_id = $1)"
                              + " GROUP BY COALESCE(session_id, trace_id)";

            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

            return ExecuteSessionQuery(cmd);
        }, ct).ConfigureAwait(false);

        return results.Count > 0 ? results[0] : null;
    }

    public Task<SessionStatsRow> GetSessionStatsAsync(
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              WITH sessions AS (
                                  SELECT
                                      COALESCE(session_id, trace_id) AS session_id,
                                      MIN(start_time_unix_nano) AS start_time_unix_nano,
                                      MAX(end_time_unix_nano) AS end_time_unix_nano,
                                      COUNT(*) AS span_count,
                                      SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                                      COUNT(CASE WHEN gen_ai_provider_name IS NOT NULL THEN 1 END) AS genai_request_count
                                  FROM spans
                                  WHERE ($1::UBIGINT IS NULL OR start_time_unix_nano >= $1)
                                    AND ($2::UBIGINT IS NULL OR start_time_unix_nano <= $2)
                                  GROUP BY COALESCE(session_id, trace_id)
                              )
                              SELECT
                                  COUNT(*) AS total_sessions,
                                  COUNT(CASE WHEN end_time_unix_nano >= $3 THEN 1 END) AS active_sessions,
                                  COALESCE(AVG(CASE
                                      WHEN end_time_unix_nano >= start_time_unix_nano
                                          THEN CAST(end_time_unix_nano - start_time_unix_nano AS DOUBLE)
                                      ELSE 0
                                  END), 0) AS avg_duration_ns,
                                  COALESCE(SUM(CASE WHEN error_count > 0 THEN 1 ELSE 0 END), 0) AS sessions_with_errors,
                                  COALESCE(SUM(CASE WHEN genai_request_count > 0 THEN 1 ELSE 0 END), 0) AS sessions_with_genai,
                                  COALESCE(SUM(CASE WHEN span_count <= 1 THEN 1 ELSE 0 END), 0) AS bounced_sessions
                              FROM sessions
                              """;

            AddUnixNanoParam(cmd, startTime);
            AddUnixNanoParam(cmd, endTime);
            AddUnixNanoParam(cmd, TimeProvider.System.GetUtcNow().AddMinutes(-30));

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return new SessionStatsRow();

            var totalSessions = reader.Col(0).GetInt64(0);
            var bouncedSessions = reader.Col(5).GetInt64(0);
            return new SessionStatsRow
            {
                TotalSessions = totalSessions,
                ActiveSessions = reader.Col(1).GetInt64(0),
                AvgDurationMs = reader.Col(2).GetDouble(0) / 1_000_000d,
                SessionsWithErrors = reader.Col(3).GetInt64(0),
                SessionsWithGenAi = reader.Col(4).GetInt64(0),
                BounceRate = totalSessions > 0 ? (double)bouncedSessions / totalSessions : 0
            };
        }, ct);
    }

    private static void AddUnixNanoParam(DuckDBCommand cmd, DateTimeOffset? value)
    {
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = value.HasValue
                ? (decimal)QylTimeConversions.ToUnixNanoUnsigned(value.Value.ToUniversalTime())
                : DBNull.Value
        });
    }

    private static List<SessionQueryRow> ExecuteSessionQuery(DbCommand cmd)
    {
        var sessions = new List<SessionQueryRow>();
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var startTimeNano = reader.Col(1).GetUInt64(0);
            var lastActivityNano = reader.Col(2).GetUInt64(0);
            var startTime = QylTimeConversions.UnixNanoToDateTime(startTimeNano);
            var lastActivity = QylTimeConversions.UnixNanoToDateTime(lastActivityNano);

            var spanCount = reader.Col(3).GetInt64(0);
            var errorCount = reader.Col(5).GetInt64(0);
            var inputTokens = reader.Col(6).GetInt64(0);
            var outputTokens = reader.Col(7).GetInt64(0);

            sessions.Add(new SessionQueryRow
            {
                SessionId = reader.GetString(0),
                StartTime = startTime,
                LastActivity = lastActivity,
                DurationMs = QylTimeConversions.NanosToMs(lastActivityNano - startTimeNano),
                SpanCount = spanCount,
                TraceCount = reader.Col(4).GetInt64(0),
                ErrorCount = errorCount,
                ErrorRate = spanCount > 0 ? (double)errorCount / spanCount : 0,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                GenAiRequestCount = reader.Col(8).GetInt64(0),
                TotalCostUsd = reader.Col(9).GetDouble(0),
                Providers = ReadStringList(reader, 10),
                Models = ReadStringList(reader, 11),
                Services = ReadStringList(reader, 12)
            });
        }

        return sessions;
    }

    private static IReadOnlyList<string> ReadStringList(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return [];

        var value = reader.GetValue(ordinal);
        return value switch
        {
            IReadOnlyList<string> list => list,
            object[] array => Array.ConvertAll(array, static item => item.ToString() ?? ""),
            _ => []
        };
    }
}

internal sealed record SessionQueryRow
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
    public IReadOnlyList<string> Providers { get; init; } = [];
    public IReadOnlyList<string> Models { get; init; } = [];
    public IReadOnlyList<string> Services { get; init; } = [];
}
