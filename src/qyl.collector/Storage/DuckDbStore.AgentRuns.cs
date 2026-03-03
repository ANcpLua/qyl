using qyl.protocol.Copilot;

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
                                   duration_ns, error_message, metadata_json, track_mode,
                                   approval_status, evidence_count)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20)
                              ON CONFLICT (run_id) DO UPDATE SET
                                  trace_id = EXCLUDED.trace_id,
                                  parent_run_id = EXCLUDED.parent_run_id,
                                  agent_name = EXCLUDED.agent_name,
                                  agent_type = EXCLUDED.agent_type,
                                  model = EXCLUDED.model,
                                  provider = EXCLUDED.provider,
                                  status = EXCLUDED.status,
                                  input_tokens = EXCLUDED.input_tokens,
                                  output_tokens = EXCLUDED.output_tokens,
                                  total_cost = EXCLUDED.total_cost,
                                  tool_call_count = EXCLUDED.tool_call_count,
                                  start_time = EXCLUDED.start_time,
                                  end_time = EXCLUDED.end_time,
                                  duration_ns = EXCLUDED.duration_ns,
                                  error_message = EXCLUDED.error_message,
                                  metadata_json = EXCLUDED.metadata_json,
                                  track_mode = EXCLUDED.track_mode,
                                  approval_status = EXCLUDED.approval_status,
                                  evidence_count = EXCLUDED.evidence_count
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
        string? trackMode = null,
        string? approvalStatus = null,
        int? evidenceCount = null,
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
                                   error_message = $8,
                                   track_mode = COALESCE($9, track_mode),
                                   approval_status = COALESCE($10, approval_status),
                                   evidence_count = COALESCE($11, evidence_count)
                              WHERE run_id = $12
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
            cmd.Parameters.Add(new DuckDBParameter { Value = trackMode ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = approvalStatus ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = evidenceCount ?? (object)DBNull.Value });
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
        string? trackMode = null,
        string? approvalStatus = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var qb = new QueryBuilder();

        if (!string.IsNullOrEmpty(agentName))
            qb.Add("agent_name = $N", agentName);
        if (!string.IsNullOrEmpty(status))
            qb.Add("status = $N", status);
        if (!string.IsNullOrEmpty(trackMode))
            qb.Add("track_mode = $N", trackMode);
        if (!string.IsNullOrEmpty(approvalStatus))
            qb.Add("approval_status = $N", approvalStatus);

        var clampedLimit = Math.Clamp(limit, 1, 1000);
        var clampedOffset = Math.Max(offset, 0);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT run_id, trace_id, parent_run_id, agent_name, agent_type,
                                  model, provider, status, input_tokens, output_tokens,
                                  total_cost, tool_call_count, start_time, end_time,
                                  duration_ns, error_message, metadata_json, track_mode,
                                  approval_status, evidence_count
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
                                 duration_ns, error_message, metadata_json, track_mode,
                                 approval_status, evidence_count
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
                                 duration_ns, error_message, metadata_json, track_mode,
                                 approval_status, evidence_count
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
    // Agent Decision Operations
    // ==========================================================================

    /// <summary>
    ///     Inserts or updates a decision event associated with an agent run.
    /// </summary>
    public async Task InsertAgentDecisionAsync(AgentDecisionRecord decision, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO agent_decisions
                                  (decision_id, run_id, trace_id, decision_type, outcome,
                                   requires_approval, approval_status, reason, evidence_json,
                                   metadata_json, created_at_unix_nano)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
                              ON CONFLICT (decision_id) DO UPDATE SET
                                  run_id = EXCLUDED.run_id,
                                  trace_id = EXCLUDED.trace_id,
                                  decision_type = EXCLUDED.decision_type,
                                  outcome = EXCLUDED.outcome,
                                  requires_approval = EXCLUDED.requires_approval,
                                  approval_status = EXCLUDED.approval_status,
                                  reason = EXCLUDED.reason,
                                  evidence_json = EXCLUDED.evidence_json,
                                  metadata_json = EXCLUDED.metadata_json,
                                  created_at_unix_nano = EXCLUDED.created_at_unix_nano
                              """;
            AddAgentDecisionParameters(cmd, decision);
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all decisions for a run in chronological order.
    /// </summary>
    public async Task<IReadOnlyList<AgentDecisionRecord>> GetAgentDecisionsAsync(
        string runId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT decision_id, run_id, trace_id, decision_type, outcome,
                                 requires_approval, approval_status, reason, evidence_json,
                                 metadata_json, created_at_unix_nano
                          FROM agent_decisions
                          WHERE run_id = $1
                          ORDER BY created_at_unix_nano ASC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });

        var decisions = new List<AgentDecisionRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            decisions.Add(MapAgentDecision(reader));

        return decisions;
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
        cmd.Parameters.Add(new DuckDBParameter { Value = run.TrackMode ?? "auto" });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.ApprovalStatus ?? "not_required" });
        cmd.Parameters.Add(new DuckDBParameter { Value = run.EvidenceCount });
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

    private static void AddAgentDecisionParameters(DuckDBCommand cmd, AgentDecisionRecord decision)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = decision.DecisionId });
        cmd.Parameters.Add(new DuckDBParameter { Value = decision.RunId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = decision.TraceId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = decision.DecisionType ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = decision.Outcome ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = decision.RequiresApproval });
        cmd.Parameters.Add(new DuckDBParameter { Value = decision.ApprovalStatus ?? "not_required" });
        cmd.Parameters.Add(new DuckDBParameter { Value = decision.Reason ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = decision.EvidenceJson ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = decision.MetadataJson ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = decision.CreatedAtUnixNano.HasValue ? (decimal)decision.CreatedAtUnixNano.Value : DBNull.Value
        });
    }

    private static AgentRunRecord MapAgentRun(DbDataReader reader) =>
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
            MetadataJson = reader.Col(16).AsString,
            TrackMode = reader.Col(17).AsString ?? "auto",
            ApprovalStatus = reader.Col(18).AsString ?? "not_required",
            EvidenceCount = reader.Col(19).GetInt32(0)
        };

    private static ToolCallRecord MapToolCall(DbDataReader reader) =>
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

    private static AgentDecisionRecord MapAgentDecision(DbDataReader reader) =>
        new()
        {
            DecisionId = reader.GetString(0),
            RunId = reader.Col(1).AsString,
            TraceId = reader.Col(2).AsString,
            DecisionType = reader.Col(3).AsString,
            Outcome = reader.Col(4).AsString,
            RequiresApproval = !reader.IsDBNull(5) && reader.GetBoolean(5),
            ApprovalStatus = reader.Col(6).AsString,
            Reason = reader.Col(7).AsString,
            EvidenceJson = reader.Col(8).AsString,
            MetadataJson = reader.Col(9).AsString,
            CreatedAtUnixNano = reader.Col(10).AsUInt64
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
    public string? TrackMode { get; init; }
    public string? ApprovalStatus { get; init; }
    public int EvidenceCount { get; init; }
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

/// <summary>
///     Storage record for a decision emitted by an agent run.
///     Maps to the agent_decisions DuckDB table.
/// </summary>
public sealed record AgentDecisionRecord
{
    public required string DecisionId { get; init; }
    public string? RunId { get; init; }
    public string? TraceId { get; init; }
    public string? DecisionType { get; init; }
    public string? Outcome { get; init; }
    public bool RequiresApproval { get; init; }
    public string? ApprovalStatus { get; init; }
    public string? Reason { get; init; }
    public string? EvidenceJson { get; init; }
    public string? MetadataJson { get; init; }
    public ulong? CreatedAtUnixNano { get; init; }
}
