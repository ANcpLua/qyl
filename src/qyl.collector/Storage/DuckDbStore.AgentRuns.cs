namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with agent run and tool call operations.
/// </summary>
public sealed partial class DuckDbStore
{
    // ==========================================================================
    // Agent Run Operations
    // ==========================================================================

    /// <summary>
    ///     Inserts a new agent run record via the channel-buffered write path.
    /// </summary>
    public async Task InsertAgentRunAsync(AgentRunRecord run, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO agent_runs
                                  (run_id, trace_id, parent_run_id, agent_name, agent_type,
                                   model, provider, status, input_tokens, output_tokens,
                                   total_cost, tool_call_count, start_time, end_time,
                                   duration_ns, error_message, metadata_json)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17)
                              ON CONFLICT (run_id) DO NOTHING
                              """;
            AddAgentRunParameters(cmd, run);
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Updates an existing agent run (status, tokens, cost) on completion.
    /// </summary>
    public async Task UpdateAgentRunAsync(
        string runId,
        string status,
        long inputTokens,
        long outputTokens,
        double totalCost,
        int toolCallCount,
        ulong? endTime,
        long? durationNs,
        string? errorMessage,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE agent_runs SET
                                  status = $1,
                                  input_tokens = $2,
                                  output_tokens = $3,
                                  total_cost = $4,
                                  tool_call_count = $5,
                                  end_time = $6,
                                  duration_ns = $7,
                                  error_message = $8
                              WHERE run_id = $9
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            cmd.Parameters.Add(new DuckDBParameter { Value = inputTokens });
            cmd.Parameters.Add(new DuckDBParameter { Value = outputTokens });
            cmd.Parameters.Add(new DuckDBParameter { Value = totalCost });
            cmd.Parameters.Add(new DuckDBParameter { Value = toolCallCount });
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = endTime.HasValue ? (decimal)endTime.Value : DBNull.Value
            });
            cmd.Parameters.Add(new DuckDBParameter { Value = durationNs ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = errorMessage ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = runId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists agent runs with optional filtering by agent name and status.
    /// </summary>
    public async Task<IReadOnlyList<AgentRunRecord>> GetAgentRunsAsync(
        int limit = 50,
        int offset = 0,
        string? agentName = null,
        string? status = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var qb = new QueryBuilder();

        if (!string.IsNullOrEmpty(agentName))
            qb.Add("agent_name = $N", agentName);
        if (!string.IsNullOrEmpty(status))
            qb.Add("status = $N", status);

        var clampedLimit = Math.Clamp(limit, 1, 1000);
        var clampedOffset = Math.Max(offset, 0);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT run_id, trace_id, parent_run_id, agent_name, agent_type,
                                  model, provider, status, input_tokens, output_tokens,
                                  total_cost, tool_call_count, start_time, end_time,
                                  duration_ns, error_message, metadata_json
                           FROM agent_runs
                           {qb.WhereClause}
                           ORDER BY start_time DESC
                           LIMIT {clampedLimit} OFFSET {clampedOffset}
                           """;

        qb.ApplyTo(cmd);

        var runs = new List<AgentRunRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            runs.Add(MapAgentRun(reader));

        return runs;
    }

    /// <summary>
    ///     Gets a single agent run by its run ID.
    /// </summary>
    public async Task<AgentRunRecord?> GetAgentRunAsync(string runId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT run_id, trace_id, parent_run_id, agent_name, agent_type,
                                 model, provider, status, input_tokens, output_tokens,
                                 total_cost, tool_call_count, start_time, end_time,
                                 duration_ns, error_message, metadata_json
                          FROM agent_runs
                          WHERE run_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return MapAgentRun(reader);

        return null;
    }

    /// <summary>
    ///     Finds agent runs associated with a specific trace ID.
    /// </summary>
    public async Task<IReadOnlyList<AgentRunRecord>> GetAgentRunsByTraceAsync(
        string traceId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT run_id, trace_id, parent_run_id, agent_name, agent_type,
                                 model, provider, status, input_tokens, output_tokens,
                                 total_cost, tool_call_count, start_time, end_time,
                                 duration_ns, error_message, metadata_json
                          FROM agent_runs
                          WHERE trace_id = $1
                          ORDER BY start_time ASC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = traceId });

        var runs = new List<AgentRunRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            runs.Add(MapAgentRun(reader));

        return runs;
    }

    // ==========================================================================
    // Tool Call Operations
    // ==========================================================================

    /// <summary>
    ///     Inserts a new tool call record via the channel-buffered write path.
    /// </summary>
    public async Task InsertToolCallAsync(ToolCallRecord toolCall, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO tool_calls
                                  (call_id, run_id, trace_id, span_id, tool_name, tool_type,
                                   arguments_json, result_json, status, start_time, end_time,
                                   duration_ns, error_message, sequence_number)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
                              ON CONFLICT (call_id) DO NOTHING
                              """;
            AddToolCallParameters(cmd, toolCall);
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets all tool calls for a specific agent run, ordered by sequence number.
    /// </summary>
    public async Task<IReadOnlyList<ToolCallRecord>> GetToolCallsAsync(
        string runId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT call_id, run_id, trace_id, span_id, tool_name, tool_type,
                                 arguments_json, result_json, status, start_time, end_time,
                                 duration_ns, error_message, sequence_number
                          FROM tool_calls
                          WHERE run_id = $1
                          ORDER BY sequence_number ASC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });

        var calls = new List<ToolCallRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            calls.Add(MapToolCall(reader));

        return calls;
    }

    // ==========================================================================
    // Private Methods - Agent Run Mapping
    // ==========================================================================

    private static void AddAgentRunParameters(DuckDBCommand cmd, AgentRunRecord run)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = run.RunId });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.TraceId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.ParentRunId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.AgentName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.AgentType ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.Model ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.Provider ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.Status ?? "running" });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.InputTokens });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.OutputTokens });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.TotalCost });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.ToolCallCount });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = run.StartTime.HasValue ? (decimal)run.StartTime.Value : DBNull.Value
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = run.EndTime.HasValue ? (decimal)run.EndTime.Value : DBNull.Value
        });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.DurationNs ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.ErrorMessage ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.MetadataJson ?? (object)DBNull.Value });
    }

    private static void AddToolCallParameters(DuckDBCommand cmd, ToolCallRecord call)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = call.CallId });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.RunId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.TraceId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.SpanId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.ToolName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.ToolType ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.ArgumentsJson ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.ResultJson ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.Status ?? "running" });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = call.StartTime.HasValue ? (decimal)call.StartTime.Value : DBNull.Value
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = call.EndTime.HasValue ? (decimal)call.EndTime.Value : DBNull.Value
        });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.DurationNs ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.ErrorMessage ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = call.SequenceNumber });
    }

    private static AgentRunRecord MapAgentRun(IDataReader reader) =>
        new()
        {
            RunId = reader.GetString(0),
            TraceId = reader.Col(1).AsString,
            ParentRunId = reader.Col(2).AsString,
            AgentName = reader.Col(3).AsString,
            AgentType = reader.Col(4).AsString,
            Model = reader.Col(5).AsString,
            Provider = reader.Col(6).AsString,
            Status = reader.Col(7).AsString,
            InputTokens = reader.Col(8).GetInt64(0),
            OutputTokens = reader.Col(9).GetInt64(0),
            TotalCost = reader.Col(10).GetDouble(0),
            ToolCallCount = reader.Col(11).GetInt32(0),
            StartTime = reader.Col(12).AsUInt64,
            EndTime = reader.Col(13).AsUInt64,
            DurationNs = reader.Col(14).AsInt64,
            ErrorMessage = reader.Col(15).AsString,
            MetadataJson = reader.Col(16).AsString
        };

    private static ToolCallRecord MapToolCall(IDataReader reader) =>
        new()
        {
            CallId = reader.GetString(0),
            RunId = reader.Col(1).AsString,
            TraceId = reader.Col(2).AsString,
            SpanId = reader.Col(3).AsString,
            ToolName = reader.Col(4).AsString,
            ToolType = reader.Col(5).AsString,
            ArgumentsJson = reader.Col(6).AsString,
            ResultJson = reader.Col(7).AsString,
            Status = reader.Col(8).AsString,
            StartTime = reader.Col(9).AsUInt64,
            EndTime = reader.Col(10).AsUInt64,
            DurationNs = reader.Col(11).AsInt64,
            ErrorMessage = reader.Col(12).AsString,
            SequenceNumber = reader.Col(13).GetInt32(0)
        };
}

// =============================================================================
// Agent Run & Tool Call Records
// =============================================================================

/// <summary>
///     Storage record for an agent run. Maps to the agent_runs DuckDB table.
/// </summary>
public sealed record AgentRunRecord
{
    public required string RunId { get; init; }
    public string? TraceId { get; init; }
    public string? ParentRunId { get; init; }
    public string? AgentName { get; init; }
    public string? AgentType { get; init; }
    public string? Model { get; init; }
    public string? Provider { get; init; }
    public string? Status { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public double TotalCost { get; init; }
    public int ToolCallCount { get; init; }
    public ulong? StartTime { get; init; }
    public ulong? EndTime { get; init; }
    public long? DurationNs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? MetadataJson { get; init; }
}

/// <summary>
///     Storage record for a tool call. Maps to the tool_calls DuckDB table.
/// </summary>
public sealed record ToolCallRecord
{
    public required string CallId { get; init; }
    public string? RunId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ToolName { get; init; }
    public string? ToolType { get; init; }
    public string? ArgumentsJson { get; init; }
    public string? ResultJson { get; init; }
    public string? Status { get; init; }
    public ulong? StartTime { get; init; }
    public ulong? EndTime { get; init; }
    public long? DurationNs { get; init; }
    public string? ErrorMessage { get; init; }
    public int SequenceNumber { get; init; }
}
