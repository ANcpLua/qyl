using qyl.collector.ClaudeCode;

namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with Claude Code session queries.
///     All data comes from the logs table where service_name = 'claude-code'.
///     Event attributes are extracted from the attributes_json JSON column using DuckDB json_extract_string().
/// </summary>
public sealed partial class DuckDbStore
{
    private const string ClaudeCodeServiceFilter = "service_name = 'claude-code'";

    public async Task<IReadOnlyList<ClaudeCodeSession>> GetClaudeCodeSessionsAsync(
        int limit = 50,
        string? afterSessionId = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var qb = new QueryBuilder();
        qb.AddCondition(ClaudeCodeServiceFilter);

        if (!string.IsNullOrEmpty(afterSessionId))
            qb.Add("session_id != $N", afterSessionId);

        var clampedLimit = Math.Clamp(limit, 1, 500);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT
                               session_id,
                               MIN(time_unix_nano) AS start_nano,
                               MAX(time_unix_nano) AS end_nano,
                               COUNT(*) FILTER (WHERE json_extract_string(attributes_json, '$.event.name') IN ('prompt', 'human_turn_start')) AS prompt_count,
                               COUNT(*) FILTER (WHERE json_extract_string(attributes_json, '$.event.name') IN ('api_request', 'api_response')) AS api_count,
                               COUNT(*) FILTER (WHERE json_extract_string(attributes_json, '$.event.name') IN ('tool_use', 'tool_result')) AS tool_count,
                               COALESCE(SUM(CAST(json_extract_string(attributes_json, '$.cost_usd') AS DOUBLE)), 0) AS total_cost,
                               COALESCE(SUM(CAST(json_extract_string(attributes_json, '$.input_tokens') AS BIGINT)), 0) AS input_tokens,
                               COALESCE(SUM(CAST(json_extract_string(attributes_json, '$.output_tokens') AS BIGINT)), 0) AS output_tokens,
                               LIST(DISTINCT json_extract_string(attributes_json, '$.model')) FILTER (WHERE json_extract_string(attributes_json, '$.model') IS NOT NULL) AS models,
                               MAX(json_extract_string(attributes_json, '$.terminal_type')) AS terminal_type,
                               MAX(json_extract_string(attributes_json, '$.claude_code_version')) AS cc_version
                           FROM logs
                           {qb.WhereClause}
                             AND session_id IS NOT NULL
                           GROUP BY session_id
                           ORDER BY end_nano DESC
                           LIMIT {clampedLimit}
                           """;

        qb.ApplyTo(cmd);

        var sessions = new List<ClaudeCodeSession>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var startNano = reader.Col(1).GetInt64(0);
            var endNano = reader.Col(2).GetInt64(0);
            var modelList = reader.Col(9).AsList<string>();

            sessions.Add(new ClaudeCodeSession
            {
                SessionId = reader.GetString(0),
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(startNano / 1_000_000),
                LastActivityTime = DateTimeOffset.FromUnixTimeMilliseconds(endNano / 1_000_000),
                TotalPrompts = reader.Col(3).GetInt32(0),
                TotalApiCalls = reader.Col(4).GetInt32(0),
                TotalToolCalls = reader.Col(5).GetInt32(0),
                TotalCostUsd = reader.Col(6).GetDouble(0),
                TotalInputTokens = reader.Col(7).GetInt64(0),
                TotalOutputTokens = reader.Col(8).GetInt64(0),
                Models = modelList ?? [],
                TerminalType = reader.Col(10).AsString,
                ClaudeCodeVersion = reader.Col(11).AsString
            });
        }

        return sessions;
    }

    public async Task<IReadOnlyList<ClaudeCodeEvent>> GetClaudeCodeTimelineAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              json_extract_string(attributes_json, '$.event.name') AS event_name,
                              json_extract_string(attributes_json, '$.prompt.id') AS prompt_id,
                              time_unix_nano,
                              json_extract_string(attributes_json, '$.tool_name') AS tool_name,
                              json_extract_string(attributes_json, '$.model') AS model,
                              CAST(json_extract_string(attributes_json, '$.cost_usd') AS DOUBLE) AS cost_usd,
                              CAST(json_extract_string(attributes_json, '$.duration_ms') AS DOUBLE) AS duration_ms,
                              CAST(json_extract_string(attributes_json, '$.input_tokens') AS BIGINT) AS input_tokens,
                              CAST(json_extract_string(attributes_json, '$.output_tokens') AS BIGINT) AS output_tokens,
                              json_extract_string(attributes_json, '$.success') AS success,
                              json_extract_string(attributes_json, '$.decision') AS decision,
                              json_extract_string(attributes_json, '$.error') AS error_text,
                              CAST(json_extract_string(attributes_json, '$.prompt_length') AS INTEGER) AS prompt_length
                          FROM logs
                          WHERE service_name = 'claude-code'
                            AND session_id = $1
                            AND json_extract_string(attributes_json, '$.event.name') IS NOT NULL
                          ORDER BY time_unix_nano ASC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        var events = new List<ClaudeCodeEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var timeNano = reader.Col(2).GetInt64(0);
            var successStr = reader.Col(9).AsString;

            events.Add(new ClaudeCodeEvent
            {
                EventName = reader.Col(0).GetString("unknown"),
                PromptId = reader.Col(1).AsString,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timeNano / 1_000_000),
                ToolName = reader.Col(3).AsString,
                Model = reader.Col(4).AsString,
                CostUsd = reader.Col(5).AsDouble,
                DurationMs = reader.Col(6).AsDouble,
                InputTokens = reader.Col(7).AsInt64,
                OutputTokens = reader.Col(8).AsInt64,
                Success = successStr is not null ? string.Equals(successStr, "true", StringComparison.OrdinalIgnoreCase) : null,
                Decision = reader.Col(10).AsString,
                Error = reader.Col(11).AsString,
                PromptLength = reader.Col(12).AsInt32
            });
        }

        return events;
    }

    public async Task<IReadOnlyList<ClaudeCodeToolSummary>> GetClaudeCodeToolSummaryAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              json_extract_string(attributes_json, '$.tool_name') AS tool_name,
                              COUNT(*) AS call_count,
                              COUNT(*) FILTER (WHERE json_extract_string(attributes_json, '$.success') = 'true') AS success_count,
                              COUNT(*) FILTER (WHERE json_extract_string(attributes_json, '$.success') = 'false') AS failure_count,
                              COALESCE(AVG(CAST(json_extract_string(attributes_json, '$.duration_ms') AS DOUBLE)), 0) AS avg_duration,
                              COUNT(*) FILTER (WHERE json_extract_string(attributes_json, '$.decision') = 'accept') AS accept_count,
                              COUNT(*) FILTER (WHERE json_extract_string(attributes_json, '$.decision') = 'reject') AS reject_count
                          FROM logs
                          WHERE service_name = 'claude-code'
                            AND session_id = $1
                            AND json_extract_string(attributes_json, '$.tool_name') IS NOT NULL
                          GROUP BY tool_name
                          ORDER BY call_count DESC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        var tools = new List<ClaudeCodeToolSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            tools.Add(new ClaudeCodeToolSummary
            {
                ToolName = reader.Col(0).GetString("unknown"),
                CallCount = reader.Col(1).GetInt32(0),
                SuccessCount = reader.Col(2).GetInt32(0),
                FailureCount = reader.Col(3).GetInt32(0),
                AvgDurationMs = reader.Col(4).GetDouble(0),
                AcceptCount = reader.Col(5).GetInt32(0),
                RejectCount = reader.Col(6).GetInt32(0)
            });
        }

        return tools;
    }

    public async Task<IReadOnlyList<ClaudeCodeCostBreakdown>> GetClaudeCodeCostBreakdownAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              json_extract_string(attributes_json, '$.model') AS model,
                              COUNT(*) AS api_calls,
                              COALESCE(SUM(CAST(json_extract_string(attributes_json, '$.cost_usd') AS DOUBLE)), 0) AS total_cost,
                              COALESCE(SUM(CAST(json_extract_string(attributes_json, '$.input_tokens') AS BIGINT)), 0) AS input_tokens,
                              COALESCE(SUM(CAST(json_extract_string(attributes_json, '$.output_tokens') AS BIGINT)), 0) AS output_tokens,
                              COALESCE(SUM(CAST(json_extract_string(attributes_json, '$.cache_read_tokens') AS BIGINT)), 0) AS cache_read,
                              COALESCE(SUM(CAST(json_extract_string(attributes_json, '$.cache_creation_tokens') AS BIGINT)), 0) AS cache_creation
                          FROM logs
                          WHERE service_name = 'claude-code'
                            AND session_id = $1
                            AND json_extract_string(attributes_json, '$.model') IS NOT NULL
                          GROUP BY model
                          ORDER BY total_cost DESC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        var breakdown = new List<ClaudeCodeCostBreakdown>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            breakdown.Add(new ClaudeCodeCostBreakdown
            {
                Model = reader.Col(0).GetString("unknown"),
                ApiCalls = reader.Col(1).GetInt32(0),
                TotalCostUsd = reader.Col(2).GetDouble(0),
                InputTokens = reader.Col(3).GetInt64(0),
                OutputTokens = reader.Col(4).GetInt64(0),
                CacheReadTokens = reader.Col(5).GetInt64(0),
                CacheCreationTokens = reader.Col(6).GetInt64(0)
            });
        }

        return breakdown;
    }
}
