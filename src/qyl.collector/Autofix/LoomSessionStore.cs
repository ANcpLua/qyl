using Qyl.Contracts.Autofix;

namespace Qyl.Collector.Autofix;

/// <summary>
///     Persistent store for <see cref="LoomSession" /> and <see cref="LoomMessage" /> records
///     backed by the <c>loom_sessions</c> and <c>loom_messages</c> DuckDB tables.
/// </summary>
public sealed class LoomSessionStore(DuckDbStore store, TimeProvider timeProvider)
{
    // =========================================================================
    // Write operations
    // =========================================================================

    /// <summary>Creates a new session record and returns it.</summary>
    public async Task<LoomSession> CreateAsync(
        string issueId,
        LoomSessionMode mode = LoomSessionMode.Interactive,
        CancellationToken ct = default)
    {
        var sessionId = $"loom-{Guid.NewGuid():N}";
        var now = timeProvider.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000L;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO loom_sessions
                    (session_id, issue_id, mode, stage, stage_name, status, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
            cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            cmd.Parameters.Add(new DuckDBParameter { Value = mode.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = (int)LoomStage.Idle });
            cmd.Parameters.Add(new DuckDBParameter { Value = LoomStage.Idle.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = LoomStatus.Idle.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return new LoomSession
        {
            SessionId = sessionId,
            IssueId = issueId,
            Mode = mode,
            Stage = LoomStage.Idle,
            Status = LoomStatus.Idle,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Persists all mutable fields of an existing session.</summary>
    public Task UpdateAsync(LoomSession session, CancellationToken ct = default)
    {
        var updatedAt = timeProvider.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000L;
        session.UpdatedAt = updatedAt;

        return store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                UPDATE loom_sessions SET
                    mode          = $1,
                    stage         = $2,
                    stage_name    = $3,
                    status        = $4,
                    updated_at    = $5,
                    root_cause_json = $6,
                    solution_json = $7,
                    fix_run_id    = $8,
                    pause_reason  = $9,
                    error         = $10
                WHERE session_id = $11
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = session.Mode.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = (int)session.Stage });
            cmd.Parameters.Add(new DuckDBParameter { Value = session.Stage.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = session.Status.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = updatedAt });
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = session.RootCause is not null
                    ? JsonSerializer.Serialize(session.RootCause)
                    : (object)DBNull.Value
            });
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = session.Solution is not null
                    ? JsonSerializer.Serialize(session.Solution)
                    : (object)DBNull.Value
            });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)session.FixRunId ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = session.PauseReason is not null
                    ? session.PauseReason.Value.ToString().ToLowerInvariant()
                    : (object)DBNull.Value
            });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)session.Error ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = session.SessionId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <summary>Appends a message to a session's history.</summary>
    public Task AppendMessageAsync(string sessionId, LoomMessage message, CancellationToken ct = default)
    {
        var messageId = $"msg-{Guid.NewGuid():N}";
        var createdAt = timeProvider.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000L;

        return store.ExecuteWriteAsync(async (con, token) =>
        {
            // Determine next sequence number
            await using var seqCmd = con.CreateCommand();
            seqCmd.CommandText = "SELECT COALESCE(MAX(sequence), -1) + 1 FROM loom_messages WHERE session_id = $1";
            seqCmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
            var sequence = Convert.ToInt64(await seqCmd.ExecuteScalarAsync(token).ConfigureAwait(false));

            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO loom_messages
                    (message_id, session_id, role, content, tool_name, tool_args, created_at, sequence)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = messageId });
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
            cmd.Parameters.Add(new DuckDBParameter { Value = message.Role.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = message.Content });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)message.ToolName ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)message.ToolArgs ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = createdAt });
            cmd.Parameters.Add(new DuckDBParameter { Value = sequence });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    // =========================================================================
    // Read operations
    // =========================================================================

    /// <summary>Returns the session with the given ID, or <c>null</c> if not found.</summary>
    public async Task<LoomSession?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, issue_id, mode, stage, status,
                   created_at, updated_at, root_cause_json, solution_json,
                   fix_run_id, pause_reason, error
            FROM loom_sessions
            WHERE session_id = $1
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapSession(reader);
    }

    /// <summary>Returns all sessions for the given issue, ordered newest first.</summary>
    public async Task<IReadOnlyList<LoomSession>> GetByIssueAsync(string issueId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, issue_id, mode, stage, status,
                   created_at, updated_at, root_cause_json, solution_json,
                   fix_run_id, pause_reason, error
            FROM loom_sessions
            WHERE issue_id = $1
            ORDER BY created_at DESC
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });

        var results = new List<LoomSession>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapSession(reader));

        return results;
    }

    /// <summary>
    ///     Returns background sessions that have reached a terminal/handoff stage
    ///     (status = 'completed' AND stage >= <see cref="LoomStage.RootCause" />).
    /// </summary>
    public async Task<IReadOnlyList<LoomSessionSummary>> GetPendingHandoffsAsync(CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, issue_id, stage, status, mode, created_at,
                   root_cause_json IS NOT NULL AS has_root_cause,
                   solution_json   IS NOT NULL AS has_solution
            FROM loom_sessions
            WHERE mode   = 'background'
              AND status = 'completed'
              AND stage  >= $1
            ORDER BY created_at ASC
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = (int)LoomStage.RootCause });

        var results = new List<LoomSessionSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new LoomSessionSummary(
                SessionId: reader.GetString(0),
                IssueId: reader.GetString(1),
                Stage: (LoomStage)reader.GetInt32(2),
                Status: ParseStatus(reader.GetString(3)),
                Mode: reader.GetString(4) == "background"
                    ? LoomSessionMode.Background
                    : LoomSessionMode.Interactive,
                CreatedAt: reader.GetInt64(5),
                HasRootCause: reader.GetBoolean(6),
                HasSolution: reader.GetBoolean(7)));
        }

        return results;
    }

    /// <summary>Returns all messages for a session ordered by sequence.</summary>
    public async Task<IReadOnlyList<LoomMessage>> GetMessagesAsync(string sessionId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT role, content, tool_name, tool_args
            FROM loom_messages
            WHERE session_id = $1
            ORDER BY sequence ASC
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        var results = new List<LoomMessage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var role = ParseRole(reader.GetString(0));
            var content = reader.GetString(1);
            var toolName = reader.Col(2).AsString;
            var toolArgs = reader.Col(3).AsString;
            results.Add(new LoomMessage(role, content, toolName, toolArgs));
        }

        return results;
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static LoomSession MapSession(DbDataReader reader)
    {
        var rootCauseJson = reader.Col(7).AsString;
        var solutionJson = reader.Col(8).AsString;
        var pauseReasonStr = reader.Col(10).AsString;

        return new LoomSession
        {
            SessionId = reader.GetString(0),
            IssueId = reader.GetString(1),
            Mode = reader.GetString(2) == "background"
                ? LoomSessionMode.Background
                : LoomSessionMode.Interactive,
            Stage = (LoomStage)reader.GetInt32(3),
            Status = ParseStatus(reader.GetString(4)),
            CreatedAt = reader.GetInt64(5),
            UpdatedAt = reader.GetInt64(6),
            RootCause = rootCauseJson is not null
                ? JsonSerializer.Deserialize<LoomRootCauseResult>(rootCauseJson)
                : null,
            Solution = solutionJson is not null
                ? JsonSerializer.Deserialize<LoomSolutionResult>(solutionJson)
                : null,
            FixRunId = reader.Col(9).AsString,
            PauseReason = pauseReasonStr is not null ? ParsePauseReason(pauseReasonStr) : null,
            Error = reader.Col(11).AsString
        };
    }

    private static LoomStatus ParseStatus(string value) => value switch
    {
        "active"    => LoomStatus.Active,
        "paused"    => LoomStatus.Paused,
        "idle"      => LoomStatus.Idle,
        "completed" => LoomStatus.Completed,
        "failed"    => LoomStatus.Failed,
        "cancelled" => LoomStatus.Cancelled,
        _           => LoomStatus.Idle
    };

    private static LoomMessageRole ParseRole(string value) => value switch
    {
        "user"      => LoomMessageRole.User,
        "assistant" => LoomMessageRole.Assistant,
        "system"    => LoomMessageRole.System,
        "tool"      => LoomMessageRole.Tool,
        _           => LoomMessageRole.User
    };

    private static LoomPauseReason ParsePauseReason(string value) => value switch
    {
        "waitingforuser" => LoomPauseReason.WaitingForUser,
        "needmoreinfo"   => LoomPauseReason.NeedMoreInfo,
        _                => LoomPauseReason.WaitingForUser
    };
}
