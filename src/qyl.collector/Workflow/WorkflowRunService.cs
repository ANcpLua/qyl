namespace qyl.collector.Workflow;

/// <summary>
///     Service layer for the workflow engine. Operates against the
///     <c>workflow_runs</c>, <c>workflow_nodes</c>, <c>workflow_events</c>,
///     and <c>workflow_checkpoints</c> DuckDB tables.
/// </summary>
public sealed partial class WorkflowRunService(DuckDbStore store, ILogger<WorkflowRunService> logger)
{
    // ==========================================================================
    // Workflow Runs
    // ==========================================================================

    /// <summary>
    ///     Gets a single workflow run by ID.
    /// </summary>
    public async Task<WorkflowRunRow?> GetRunByIdAsync(string runId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = RunSelectSql + " WHERE id = $1";
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? MapRun(reader) : null;
    }

    /// <summary>
    ///     Lists workflow runs with optional filtering.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowRunRow>> ListRunsAsync(
        string? projectId = null,
        string? workflowId = null,
        string? status = null,
        string? triggerType = null,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        if (!string.IsNullOrEmpty(projectId))
        {
            conditions.Add($"project_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = projectId });
        }

        if (!string.IsNullOrEmpty(workflowId))
        {
            conditions.Add($"workflow_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = workflowId });
        }

        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add($"status = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = status });
        }

        if (!string.IsNullOrEmpty(triggerType))
        {
            conditions.Add($"trigger_type = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = triggerType });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";
        var clampedLimit = Math.Clamp(limit, 1, 1000);
        var clampedOffset = Math.Max(offset, 0);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            {RunSelectSql}
            {whereClause}
            ORDER BY created_at DESC
            LIMIT ${paramIndex++} OFFSET ${paramIndex}
            """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = clampedLimit });
        cmd.Parameters.Add(new DuckDBParameter { Value = clampedOffset });

        var results = new List<WorkflowRunRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapRun(reader));

        return results;
    }

    // ==========================================================================
    // Workflow Nodes
    // ==========================================================================

    /// <summary>
    ///     Gets all nodes for a workflow run, ordered by creation time.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowNodeRow>> GetNodesAsync(
        string runId,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, run_id, node_id, node_type, node_name, attempt,
                   input_json, output_json, status, error_message,
                   retry_count, max_retries, timeout_ms,
                   started_at, completed_at, duration_ms, created_at
            FROM workflow_nodes
            WHERE run_id = $1
            ORDER BY created_at ASC
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });

        var results = new List<WorkflowNodeRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapNode(reader));

        return results;
    }

    // ==========================================================================
    // Workflow Events (Event Sourcing)
    // ==========================================================================

    /// <summary>
    ///     Gets events for a workflow run, optionally after a sequence cursor for paging.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowEventRow>> GetEventsAsync(
        string runId,
        long? afterSequence = null,
        int limit = 200,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        var conditions = new List<string> { "run_id = $1" };
        var parameters = new List<DuckDBParameter> { new() { Value = runId } };
        var paramIndex = 2;

        if (afterSequence.HasValue)
        {
            conditions.Add($"sequence_number > ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = afterSequence.Value });
        }

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, run_id, node_id, event_type, event_name,
                   payload_json, sequence_number, source, correlation_id, timestamp
            FROM workflow_events
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY sequence_number ASC
            LIMIT ${paramIndex}
            """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 5000) });

        var results = new List<WorkflowEventRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapEvent(reader));

        return results;
    }

    // ==========================================================================
    // Workflow Checkpoints
    // ==========================================================================

    /// <summary>
    ///     Gets checkpoints for a workflow run, ordered by sequence number descending (latest first).
    /// </summary>
    public async Task<IReadOnlyList<WorkflowCheckpointRow>> GetCheckpointsAsync(
        string runId,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, run_id, node_id, checkpoint_type, state_json,
                   sequence_number, created_at
            FROM workflow_checkpoints
            WHERE run_id = $1
            ORDER BY sequence_number DESC
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });

        var results = new List<WorkflowCheckpointRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new WorkflowCheckpointRow
            {
                Id = reader.GetString(0),
                RunId = reader.GetString(1),
                NodeId = reader.GetString(2),
                CheckpointType = reader.GetString(3),
                StateJson = reader.Col(4).AsString,
                SequenceNumber = reader.GetInt64(5),
                CreatedAt = reader.GetDateTime(6)
            });
        }

        return results;
    }

    // ==========================================================================
    // Resume / Approve
    // ==========================================================================

    /// <summary>
    ///     Resumes a paused workflow run by setting its status to 'running'.
    /// </summary>
    public async Task<bool> ResumeRunAsync(string runId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            UPDATE workflow_runs SET status = 'running'
            WHERE id = $1 AND status IN ('paused', 'pending')
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });
        var updated = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;

        if (updated)
            LogRunResumed(runId);

        return updated;
    }

    /// <summary>
    ///     Approves a workflow node that is awaiting human approval.
    /// </summary>
    public async Task<bool> ApproveNodeAsync(string runId, string nodeId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            UPDATE workflow_nodes SET status = 'approved'
            WHERE run_id = $1 AND node_id = $2 AND status = 'awaiting_approval'
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });
        cmd.Parameters.Add(new DuckDBParameter { Value = nodeId });
        var updated = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;

        if (updated)
            LogNodeApproved(runId, nodeId);

        return updated;
    }

    /// <summary>
    ///     Cancels a running or pending workflow run.
    /// </summary>
    public async Task<bool> CancelRunAsync(string runId, CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            UPDATE workflow_runs SET
                status = 'cancelled',
                completed_at = $1
            WHERE id = $2 AND status IN ('pending', 'running', 'paused')
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = now });
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    // ==========================================================================
    // Private Methods - SQL & Mapping
    // ==========================================================================

    private const string RunSelectSql = """
        SELECT id, workflow_id, workflow_version, project_id, trigger_type,
               trigger_source, input_json, output_json, status, error_message,
               parent_run_id, correlation_id, started_at, completed_at,
               duration_ms, created_at
        FROM workflow_runs
        """;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static WorkflowRunRow MapRun(IDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            WorkflowId = reader.GetString(1),
            WorkflowVersion = reader.GetInt32(2),
            ProjectId = reader.GetString(3),
            TriggerType = reader.GetString(4),
            TriggerSource = reader.Col(5).AsString,
            InputJson = reader.Col(6).AsString,
            OutputJson = reader.Col(7).AsString,
            Status = reader.GetString(8),
            ErrorMessage = reader.Col(9).AsString,
            ParentRunId = reader.Col(10).AsString,
            CorrelationId = reader.Col(11).AsString,
            StartedAt = reader.Col(12).AsDateTime,
            CompletedAt = reader.Col(13).AsDateTime,
            DurationMs = reader.Col(14).AsInt32,
            CreatedAt = reader.GetDateTime(15)
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static WorkflowNodeRow MapNode(IDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            RunId = reader.GetString(1),
            NodeId = reader.GetString(2),
            NodeType = reader.GetString(3),
            NodeName = reader.GetString(4),
            Attempt = reader.GetInt32(5),
            InputJson = reader.Col(6).AsString,
            OutputJson = reader.Col(7).AsString,
            Status = reader.GetString(8),
            ErrorMessage = reader.Col(9).AsString,
            RetryCount = reader.GetInt32(10),
            MaxRetries = reader.GetInt32(11),
            TimeoutMs = reader.Col(12).AsInt32,
            StartedAt = reader.Col(13).AsDateTime,
            CompletedAt = reader.Col(14).AsDateTime,
            DurationMs = reader.Col(15).AsInt32,
            CreatedAt = reader.GetDateTime(16)
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static WorkflowEventRow MapEvent(IDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            RunId = reader.GetString(1),
            NodeId = reader.Col(2).AsString,
            EventType = reader.GetString(3),
            EventName = reader.GetString(4),
            PayloadJson = reader.Col(5).AsString,
            SequenceNumber = reader.GetInt64(6),
            Source = reader.Col(7).AsString,
            CorrelationId = reader.Col(8).AsString,
            Timestamp = reader.GetDateTime(9)
        };

    // ==========================================================================
    // Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information, Message = "Workflow run {RunId} resumed")]
    private partial void LogRunResumed(string runId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Workflow run {RunId} node {NodeId} approved")]
    private partial void LogNodeApproved(string runId, string nodeId);
}

// =============================================================================
// Workflow Storage Records
// =============================================================================

/// <summary>
///     Storage row for the <c>workflow_runs</c> table.
/// </summary>
public sealed record WorkflowRunRow
{
    public required string Id { get; init; }
    public required string WorkflowId { get; init; }
    public required int WorkflowVersion { get; init; }
    public required string ProjectId { get; init; }
    public required string TriggerType { get; init; }
    public string? TriggerSource { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ParentRunId { get; init; }
    public string? CorrelationId { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? DurationMs { get; init; }
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
///     Storage row for the <c>workflow_nodes</c> table.
/// </summary>
public sealed record WorkflowNodeRow
{
    public required string Id { get; init; }
    public required string RunId { get; init; }
    public required string NodeId { get; init; }
    public required string NodeType { get; init; }
    public required string NodeName { get; init; }
    public required int Attempt { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public required int RetryCount { get; init; }
    public required int MaxRetries { get; init; }
    public int? TimeoutMs { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? DurationMs { get; init; }
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
///     Storage row for the <c>workflow_events</c> table.
/// </summary>
public sealed record WorkflowEventRow
{
    public required string Id { get; init; }
    public required string RunId { get; init; }
    public string? NodeId { get; init; }
    public required string EventType { get; init; }
    public required string EventName { get; init; }
    public string? PayloadJson { get; init; }
    public required long SequenceNumber { get; init; }
    public string? Source { get; init; }
    public string? CorrelationId { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
///     Storage row for the <c>workflow_checkpoints</c> table.
/// </summary>
public sealed record WorkflowCheckpointRow
{
    public required string Id { get; init; }
    public required string RunId { get; init; }
    public required string NodeId { get; init; }
    public required string CheckpointType { get; init; }
    public string? StateJson { get; init; }
    public required long SequenceNumber { get; init; }
    public required DateTime CreatedAt { get; init; }
}
