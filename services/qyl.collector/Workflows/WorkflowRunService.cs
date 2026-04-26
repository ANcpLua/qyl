namespace Qyl.Collector.Workflows;

[QylService(QylLifetime.Singleton)]
public sealed class WorkflowRunService(DuckDbStore store)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 1000;

    private const string RunSelectSql = """
                                        SELECT id, workflow_id, workflow_version, project_id, trigger_type,
                                               trigger_source, input_json, output_json, status, error_message,
                                               parent_run_id, correlation_id, started_at, completed_at,
                                               duration_ms, created_at
                                        FROM workflow_runs
                                        """;

    public async Task<CursorPage<WorkflowRunEntity>> ListRunsAsync(
        string? projectId,
        string? workflowId,
        string? status,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int? limit,
        string? cursor,
        CancellationToken ct = default)
    {
        var pageSize = ClampLimit(limit);
        var offset = DecodeOffset(cursor);
        var (whereClause, parameters) = BuildRunFilters(projectId, workflowId, status, startTime, endTime);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = string.Concat(
            RunSelectSql, "\n",
            whereClause, "\n",
            "ORDER BY created_at DESC\n",
            "LIMIT $", (parameters.Count + 1).ToString(CultureInfo.InvariantCulture),
            " OFFSET $", (parameters.Count + 2).ToString(CultureInfo.InvariantCulture));

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = pageSize + 1 });
        cmd.Parameters.Add(new DuckDBParameter { Value = offset });

        var rows = new List<WorkflowRunEntity>(pageSize + 1);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(MapRun(reader));

        var hasMore = rows.Count > pageSize;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);

        return new CursorPage<WorkflowRunEntity>(
            [.. rows],
            hasMore ? EncodeOffset(offset + pageSize) : null,
            offset > 0 ? EncodeOffset(Math.Max(0, offset - pageSize)) : null,
            hasMore);
    }

    public async Task<WorkflowRunEntity?> GetRunAsync(string runId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = RunSelectSql + " WHERE id = $1";
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? MapRun(reader) : null;
    }

    public async Task<CursorPage<WorkflowNodeEntity>> GetRunNodesAsync(
        string runId,
        int? limit,
        string? cursor,
        CancellationToken ct = default)
    {
        var pageSize = ClampLimit(limit);
        var offset = DecodeOffset(cursor);

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
                          LIMIT $2 OFFSET $3
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });
        cmd.Parameters.Add(new DuckDBParameter { Value = pageSize + 1 });
        cmd.Parameters.Add(new DuckDBParameter { Value = offset });

        var rows = new List<WorkflowNodeEntity>(pageSize + 1);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(MapNode(reader));

        var hasMore = rows.Count > pageSize;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);

        return new CursorPage<WorkflowNodeEntity>(
            [.. rows],
            hasMore ? EncodeOffset(offset + pageSize) : null,
            offset > 0 ? EncodeOffset(Math.Max(0, offset - pageSize)) : null,
            hasMore);
    }

    public async Task<IReadOnlyList<WorkflowEventEntity>> GetRunEventsAsync(
        string runId,
        long? afterSequence,
        int? limit,
        CancellationToken ct = default)
    {
        var pageSize = ClampLimit(limit, 200, 5000);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = afterSequence.HasValue
            ? """
              SELECT id, run_id, node_id, event_type, event_name,
                     payload_json, sequence_number, source, correlation_id, timestamp
              FROM workflow_events
              WHERE run_id = $1
                AND sequence_number > $2
              ORDER BY sequence_number ASC
              LIMIT $3
              """
            : """
              SELECT id, run_id, node_id, event_type, event_name,
                     payload_json, sequence_number, source, correlation_id, timestamp
              FROM workflow_events
              WHERE run_id = $1
              ORDER BY sequence_number ASC
              LIMIT $2
              """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });
        if (afterSequence.HasValue)
            cmd.Parameters.Add(new DuckDBParameter { Value = afterSequence.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = pageSize });

        var rows = new List<WorkflowEventEntity>(pageSize);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(MapEvent(reader));

        return rows;
    }

    public async Task<IReadOnlyList<WorkflowCheckpointEntity>> GetRunCheckpointsAsync(
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

        var rows = new List<WorkflowCheckpointEntity>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new WorkflowCheckpointEntity(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.Col(4).AsString ?? string.Empty,
                reader.GetInt64(5),
                ReadDateTimeOffset(reader, 6) ?? DateTimeOffset.MinValue));
        }

        return rows;
    }

    public async Task<WorkflowRunEntity?> ResumeRunAsync(string runId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          UPDATE workflow_runs
                          SET status = 'running',
                              started_at = COALESCE(started_at, $1)
                          WHERE id = $2 AND status IN ('paused', 'pending')
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = TimeProvider.System.GetUtcNow().UtcDateTime });
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return await GetRunAsync(runId, ct).ConfigureAwait(false);
    }

    public async Task<WorkflowNodeEntity?> ApproveNodeAsync(
        string runId,
        string nodeId,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          UPDATE workflow_nodes
                          SET status = 'running',
                              started_at = COALESCE(started_at, $1)
                          WHERE run_id = $2
                            AND node_id = $3
                            AND status IN ('pending', 'paused', 'awaiting_approval')
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = TimeProvider.System.GetUtcNow().UtcDateTime });
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });
        cmd.Parameters.Add(new DuckDBParameter { Value = nodeId });
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return await GetNodeAsync(runId, nodeId, ct).ConfigureAwait(false);
    }

    public async Task<WorkflowRunEntity?> CancelRunAsync(string runId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          UPDATE workflow_runs
                          SET status = 'cancelled',
                              completed_at = COALESCE(completed_at, $1)
                          WHERE id = $2 AND status IN ('pending', 'running', 'paused')
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = TimeProvider.System.GetUtcNow().UtcDateTime });
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return await GetRunAsync(runId, ct).ConfigureAwait(false);
    }

    private async Task<WorkflowNodeEntity?> GetNodeAsync(
        string runId,
        string nodeId,
        CancellationToken ct)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, run_id, node_id, node_type, node_name, attempt,
                                 input_json, output_json, status, error_message,
                                 retry_count, max_retries, timeout_ms,
                                 started_at, completed_at, duration_ms, created_at
                          FROM workflow_nodes
                          WHERE run_id = $1 AND node_id = $2
                          ORDER BY attempt DESC, created_at DESC
                          LIMIT 1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });
        cmd.Parameters.Add(new DuckDBParameter { Value = nodeId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? MapNode(reader) : null;
    }

    private static (string WhereClause, List<DuckDBParameter> Parameters) BuildRunFilters(
        string? projectId,
        string? workflowId,
        string? status,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime)
    {
        List<string> conditions = [];
        List<DuckDBParameter> parameters = [];

        AddFilter(projectId, "project_id");
        AddFilter(workflowId, "workflow_id");
        if (NormalizeStatus(status) is { } normalizedStatus)
            AddFilter(normalizedStatus, "status");
        if (startTime.HasValue)
            AddFilter(startTime.Value.UtcDateTime, "created_at", ">=");
        if (endTime.HasValue)
            AddFilter(endTime.Value.UtcDateTime, "created_at", "<=");

        return (conditions.Count is 0 ? string.Empty : $"WHERE {string.Join(" AND ", conditions)}", parameters);

        void AddFilter(object? value, string column, string op = "=")
        {
            if (value is null) return;
            var parameterName = "$" + (parameters.Count + 1).ToString(CultureInfo.InvariantCulture);
            conditions.Add($"{column} {op} {parameterName}");
            parameters.Add(new DuckDBParameter { Value = value });
        }
    }

    private static WorkflowRunEntity MapRun(DbDataReader reader) =>
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
            StartedAt = ReadDateTimeOffset(reader, 12),
            CompletedAt = ReadDateTimeOffset(reader, 13),
            DurationMs = reader.Col(14).AsInt32,
            CreatedAt = ReadDateTimeOffset(reader, 15) ?? DateTimeOffset.MinValue
        };

    private static WorkflowNodeEntity MapNode(DbDataReader reader) =>
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
            StartedAt = ReadDateTimeOffset(reader, 13),
            CompletedAt = ReadDateTimeOffset(reader, 14),
            DurationMs = reader.Col(15).AsInt32,
            CreatedAt = ReadDateTimeOffset(reader, 16) ?? DateTimeOffset.MinValue
        };

    private static WorkflowEventEntity MapEvent(DbDataReader reader) =>
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
            Timestamp = ReadDateTimeOffset(reader, 9) ?? DateTimeOffset.MinValue
        };

    private static DateTimeOffset? ReadDateTimeOffset(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc));

    private static int ClampLimit(int? limit, int defaultLimit = DefaultLimit, int maxLimit = MaxLimit) =>
        Math.Clamp(limit ?? defaultLimit, 1, maxLimit);

    private static int DecodeOffset(string? cursor) =>
        int.TryParse(cursor, CultureInfo.InvariantCulture, out var offset)
            ? Math.Max(offset, 0)
            : 0;

    private static string EncodeOffset(int offset) => offset.ToString(CultureInfo.InvariantCulture);

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        var normalized = status.Replace('-', '_').ToLowerInvariant();
        return normalized switch
        {
            "pending" or "running" or "paused" or "completed" or "failed" or "cancelled" or "timed_out" => normalized,
            _ => null
        };
    }
}

public sealed record CursorPage<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("next_cursor")]
    string? NextCursor,
    [property: JsonPropertyName("prev_cursor")]
    string? PrevCursor,
    [property: JsonPropertyName("has_more")]
    bool HasMore);

public sealed record WorkflowRunEntity
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("workflow_id")] public required string WorkflowId { get; init; }
    [JsonPropertyName("workflow_version")] public required int WorkflowVersion { get; init; }
    [JsonPropertyName("project_id")] public required string ProjectId { get; init; }
    [JsonPropertyName("trigger_type")] public required string TriggerType { get; init; }
    [JsonPropertyName("trigger_source")] public string? TriggerSource { get; init; }
    [JsonPropertyName("input_json")] public string? InputJson { get; init; }
    [JsonPropertyName("output_json")] public string? OutputJson { get; init; }
    [JsonPropertyName("status")] public required string Status { get; init; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; init; }
    [JsonPropertyName("parent_run_id")] public string? ParentRunId { get; init; }
    [JsonPropertyName("correlation_id")] public string? CorrelationId { get; init; }
    [JsonPropertyName("started_at")] public DateTimeOffset? StartedAt { get; init; }
    [JsonPropertyName("completed_at")] public DateTimeOffset? CompletedAt { get; init; }
    [JsonPropertyName("duration_ms")] public int? DurationMs { get; init; }
    [JsonPropertyName("created_at")] public required DateTimeOffset CreatedAt { get; init; }
}

public sealed record WorkflowNodeEntity
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("run_id")] public required string RunId { get; init; }
    [JsonPropertyName("node_id")] public required string NodeId { get; init; }
    [JsonPropertyName("node_type")] public required string NodeType { get; init; }
    [JsonPropertyName("node_name")] public required string NodeName { get; init; }
    [JsonPropertyName("attempt")] public required int Attempt { get; init; }
    [JsonPropertyName("input_json")] public string? InputJson { get; init; }
    [JsonPropertyName("output_json")] public string? OutputJson { get; init; }
    [JsonPropertyName("status")] public required string Status { get; init; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; init; }
    [JsonPropertyName("retry_count")] public required int RetryCount { get; init; }
    [JsonPropertyName("max_retries")] public required int MaxRetries { get; init; }
    [JsonPropertyName("timeout_ms")] public int? TimeoutMs { get; init; }
    [JsonPropertyName("started_at")] public DateTimeOffset? StartedAt { get; init; }
    [JsonPropertyName("completed_at")] public DateTimeOffset? CompletedAt { get; init; }
    [JsonPropertyName("duration_ms")] public int? DurationMs { get; init; }
    [JsonPropertyName("created_at")] public required DateTimeOffset CreatedAt { get; init; }
}

public sealed record WorkflowEventEntity
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("run_id")] public required string RunId { get; init; }
    [JsonPropertyName("node_id")] public string? NodeId { get; init; }
    [JsonPropertyName("event_type")] public required string EventType { get; init; }
    [JsonPropertyName("event_name")] public required string EventName { get; init; }
    [JsonPropertyName("payload_json")] public string? PayloadJson { get; init; }
    [JsonPropertyName("sequence_number")] public required long SequenceNumber { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("correlation_id")] public string? CorrelationId { get; init; }
    [JsonPropertyName("timestamp")] public required DateTimeOffset Timestamp { get; init; }
}

public sealed record WorkflowCheckpointEntity(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("node_id")]
    string NodeId,
    [property: JsonPropertyName("checkpoint_type")]
    string CheckpointType,
    [property: JsonPropertyName("state_json")]
    string StateJson,
    [property: JsonPropertyName("sequence_number")]
    long SequenceNumber,
    [property: JsonPropertyName("created_at")]
    DateTimeOffset CreatedAt);
