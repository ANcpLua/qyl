namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore"/> with workflow execution,
///     checkpoint, and event operations.
/// </summary>
public sealed partial class DuckDbStore
{
    // ==========================================================================
    // Workflow Execution Operations (WorkflowExecutionRecord)
    // ==========================================================================

    /// <summary>
    ///     Inserts a workflow execution using the full record type.
    /// </summary>
    public async Task InsertWorkflowExecutionAsync(WorkflowExecutionRecord record, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO workflow_executions
                    (execution_id, trace_id, workflow_name, trigger, status,
                     input_json, output_json, gen_ai_input_tokens, gen_ai_output_tokens,
                     gen_ai_cost_usd, node_count, completed_nodes,
                     start_time_unix_nano, end_time_unix_nano, duration_ns, error_message)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16)
                ON CONFLICT (execution_id) DO NOTHING
                """;
            AddWorkflowExecutionParameters(cmd, record);
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Updates a workflow execution using the full record type.
    /// </summary>
    public async Task UpdateWorkflowExecutionAsync(WorkflowExecutionRecord record, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                UPDATE workflow_executions SET
                    status = $1,
                    output_json = $2,
                    gen_ai_input_tokens = $3,
                    gen_ai_output_tokens = $4,
                    gen_ai_cost_usd = $5,
                    completed_nodes = $6,
                    end_time_unix_nano = $7,
                    duration_ns = $8,
                    error_message = $9
                WHERE execution_id = $10
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Status ?? "pending" });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.OutputJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.GenAiInputTokens });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.GenAiOutputTokens });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.GenAiCostUsd });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.CompletedNodes });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.EndTimeUnixNano ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.DurationNs ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ErrorMessage ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ExecutionId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a single workflow execution by ID as a full record.
    /// </summary>
    public async Task<WorkflowExecutionRecord?> GetWorkflowExecutionAsync(
        string executionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = WorkflowExecutionSelectSql + " WHERE execution_id = $1";
        cmd.Parameters.Add(new DuckDBParameter { Value = executionId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return MapWorkflowExecution(reader);

        return null;
    }

    /// <summary>
    ///     Lists workflow executions with optional filtering.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowExecutionRecord>> GetWorkflowExecutionsAsync(
        int limit = 50,
        int offset = 0,
        string? workflowName = null,
        string? status = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var qb = new QueryBuilder();

        if (!string.IsNullOrEmpty(workflowName))
            qb.Add("workflow_name = $N", workflowName);
        if (!string.IsNullOrEmpty(status))
            qb.Add("status = $N", status);

        var clampedLimit = Math.Clamp(limit, 1, 1000);
        var clampedOffset = Math.Max(offset, 0);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            {WorkflowExecutionSelectSql}
            {qb.WhereClause}
            ORDER BY start_time_unix_nano DESC
            LIMIT {clampedLimit} OFFSET {clampedOffset}
            """;

        qb.ApplyTo(cmd);

        var records = new List<WorkflowExecutionRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            records.Add(MapWorkflowExecution(reader));

        return records;
    }

    // ==========================================================================
    // Workflow Checkpoint Operations
    // ==========================================================================

    /// <summary>
    ///     Inserts a workflow checkpoint record via the channel-buffered write path.
    /// </summary>
    public async Task InsertCheckpointAsync(WorkflowCheckpointRecord record, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO workflow_checkpoints
                    (checkpoint_id, execution_id, node_id, state_json,
                     sequence_number, created_at_unix_nano)
                VALUES ($1, $2, $3, $4, $5, $6)
                ON CONFLICT (checkpoint_id) DO NOTHING
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = record.CheckpointId });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ExecutionId });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.NodeId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.StateJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.SequenceNumber });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.CreatedAtUnixNano });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets all checkpoints for a workflow execution, ordered by sequence number.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowCheckpointRecord>> GetCheckpointsAsync(
        string executionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT checkpoint_id, execution_id, node_id, state_json,
                   sequence_number, created_at_unix_nano
            FROM workflow_checkpoints
            WHERE execution_id = $1
            ORDER BY sequence_number ASC
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = executionId });

        var records = new List<WorkflowCheckpointRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            records.Add(MapCheckpoint(reader));

        return records;
    }

    // ==========================================================================
    // Workflow Event Operations
    // ==========================================================================

    /// <summary>
    ///     Inserts a workflow event record via the channel-buffered write path.
    /// </summary>
    public async Task InsertWorkflowEventAsync(WorkflowEventRecord record, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO workflow_events
                    (event_id, execution_id, node_id, event_type,
                     payload_json, sequence_number, created_at_unix_nano)
                VALUES ($1, $2, $3, $4, $5, $6, $7)
                ON CONFLICT (event_id) DO NOTHING
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = record.EventId });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ExecutionId });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.NodeId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.EventType ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.PayloadJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.SequenceNumber });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.CreatedAtUnixNano });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets workflow events for an execution, ordered by sequence number.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowEventRecord>> GetWorkflowEventsAsync(
        string executionId,
        int? afterSequence = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var qb = new QueryBuilder();
        qb.Add("execution_id = $N", executionId);
        if (afterSequence.HasValue)
            qb.Add("sequence_number > $N", afterSequence.Value);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT event_id, execution_id, node_id, event_type,
                   payload_json, sequence_number, created_at_unix_nano
            FROM workflow_events
            {qb.WhereClause}
            ORDER BY sequence_number ASC
            """;

        qb.ApplyTo(cmd);

        var records = new List<WorkflowEventRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            records.Add(MapEvent(reader));

        return records;
    }

    // ==========================================================================
    // Workflow Cancel
    // ==========================================================================

    /// <summary>
    ///     Cancels a running workflow execution by setting its status to 'cancelled'.
    /// </summary>
    public async Task<bool> CancelWorkflowExecutionAsync(string executionId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                UPDATE workflow_executions
                SET status = 'cancelled',
                    end_time_unix_nano = $1
                WHERE execution_id = $2
                  AND status IN ('pending', 'running')
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000L });
            cmd.Parameters.Add(new DuckDBParameter { Value = executionId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false) > 0;
    }

    // ==========================================================================
    // Private Methods - Workflow Mapping
    // ==========================================================================

    private const string WorkflowExecutionSelectSql = """
        SELECT execution_id, trace_id, workflow_name, trigger, status,
               input_json, output_json, gen_ai_input_tokens, gen_ai_output_tokens,
               gen_ai_cost_usd, node_count, completed_nodes,
               start_time_unix_nano, end_time_unix_nano, duration_ns, error_message
        FROM workflow_executions
        """;

    private static void AddWorkflowExecutionParameters(DuckDBCommand cmd, WorkflowExecutionRecord record)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = record.ExecutionId });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.TraceId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.WorkflowName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.Trigger ?? "manual" });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.Status ?? "pending" });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.InputJson ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.OutputJson ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.GenAiInputTokens });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.GenAiOutputTokens });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.GenAiCostUsd });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.NodeCount });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.CompletedNodes });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.StartTimeUnixNano ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.EndTimeUnixNano ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.DurationNs ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = record.ErrorMessage ?? (object)DBNull.Value });
    }

    private static WorkflowExecutionRecord MapWorkflowExecution(IDataReader reader) =>
        new()
        {
            ExecutionId = reader.GetString(0),
            TraceId = reader.Col(1).AsString,
            WorkflowName = reader.Col(2).AsString,
            Trigger = reader.Col(3).AsString,
            Status = reader.Col(4).AsString,
            InputJson = reader.Col(5).AsString,
            OutputJson = reader.Col(6).AsString,
            GenAiInputTokens = reader.Col(7).GetInt64(0),
            GenAiOutputTokens = reader.Col(8).GetInt64(0),
            GenAiCostUsd = reader.Col(9).GetDouble(0),
            NodeCount = reader.Col(10).GetInt32(0),
            CompletedNodes = reader.Col(11).GetInt32(0),
            StartTimeUnixNano = reader.Col(12).AsInt64,
            EndTimeUnixNano = reader.Col(13).AsInt64,
            DurationNs = reader.Col(14).AsInt64,
            ErrorMessage = reader.Col(15).AsString
        };

    private static WorkflowCheckpointRecord MapCheckpoint(IDataReader reader) =>
        new()
        {
            CheckpointId = reader.GetString(0),
            ExecutionId = reader.Col(1).AsString,
            NodeId = reader.Col(2).AsString,
            StateJson = reader.Col(3).AsString,
            SequenceNumber = reader.Col(4).GetInt32(0),
            CreatedAtUnixNano = reader.Col(5).GetInt64(0)
        };

    private static WorkflowEventRecord MapEvent(IDataReader reader) =>
        new()
        {
            EventId = reader.GetString(0),
            ExecutionId = reader.Col(1).AsString,
            NodeId = reader.Col(2).AsString,
            EventType = reader.Col(3).AsString,
            PayloadJson = reader.Col(4).AsString,
            SequenceNumber = reader.Col(5).GetInt32(0),
            CreatedAtUnixNano = reader.Col(6).GetInt64(0)
        };
}

// =============================================================================
// Workflow Storage Records
// =============================================================================

/// <summary>
///     Storage record for a workflow execution. Maps to the workflow_executions DuckDB table.
/// </summary>
public sealed record WorkflowExecutionRecord
{
    public required string ExecutionId { get; init; }
    public string? TraceId { get; init; }
    public string? WorkflowName { get; init; }
    public string? Trigger { get; init; }
    public string? Status { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public long GenAiInputTokens { get; init; }
    public long GenAiOutputTokens { get; init; }
    public double GenAiCostUsd { get; init; }
    public int NodeCount { get; init; }
    public int CompletedNodes { get; init; }
    public long? StartTimeUnixNano { get; init; }
    public long? EndTimeUnixNano { get; init; }
    public long? DurationNs { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
///     Storage record for a workflow checkpoint. Maps to the workflow_checkpoints DuckDB table.
/// </summary>
public sealed record WorkflowCheckpointRecord
{
    public required string CheckpointId { get; init; }
    public string? ExecutionId { get; init; }
    public string? NodeId { get; init; }
    public string? StateJson { get; init; }
    public int SequenceNumber { get; init; }
    public long CreatedAtUnixNano { get; init; }
}

/// <summary>
///     Storage record for a workflow event. Maps to the workflow_events DuckDB table.
/// </summary>
public sealed record WorkflowEventRecord
{
    public required string EventId { get; init; }
    public string? ExecutionId { get; init; }
    public string? NodeId { get; init; }
    public string? EventType { get; init; }
    public string? PayloadJson { get; init; }
    public int SequenceNumber { get; init; }
    public long CreatedAtUnixNano { get; init; }
}
