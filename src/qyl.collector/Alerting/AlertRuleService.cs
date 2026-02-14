namespace qyl.collector.Alerting;

/// <summary>
///     Service layer for alert rule and firing persistence. Operates against the
///     <c>alert_rules</c>, <c>alert_firings</c>, <c>fix_runs</c>, and <c>fix_artifacts</c>
///     DuckDB tables using pooled connections.
/// </summary>
public sealed partial class AlertRuleService(DuckDbStore store, ILogger<AlertRuleService> logger)
{
    // ==========================================================================
    // Alert Rules CRUD
    // ==========================================================================

    /// <summary>
    ///     Creates a new alert rule.
    /// </summary>
    /// <returns>The newly created rule ID.</returns>
    public async Task<string> CreateRuleAsync(
        string projectId,
        string name,
        string ruleType,
        string conditionJson,
        string targetType,
        string? description = null,
        string? thresholdJson = null,
        string? targetFilterJson = null,
        string severity = "warning",
        int cooldownSeconds = 300,
        string? notificationChannelsJson = null,
        CancellationToken ct = default)
    {
        var ruleId = Guid.NewGuid().ToString("N");
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO alert_rules
                    (id, project_id, name, description, rule_type, condition_json,
                     threshold_json, target_type, target_filter_json, severity,
                     cooldown_seconds, notification_channels_json, enabled,
                     created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, TRUE, $13, $14)
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = ruleId });
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
            cmd.Parameters.Add(new DuckDBParameter { Value = name });
            cmd.Parameters.Add(new DuckDBParameter { Value = description ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = ruleType });
            cmd.Parameters.Add(new DuckDBParameter { Value = conditionJson });
            cmd.Parameters.Add(new DuckDBParameter { Value = thresholdJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = targetType });
            cmd.Parameters.Add(new DuckDBParameter { Value = targetFilterJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = severity });
            cmd.Parameters.Add(new DuckDBParameter { Value = cooldownSeconds });
            cmd.Parameters.Add(new DuckDBParameter { Value = notificationChannelsJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        LogRuleCreated(ruleId, name);
        return ruleId;
    }

    /// <summary>
    ///     Gets a single alert rule by ID.
    /// </summary>
    public async Task<AlertRuleRow?> GetRuleByIdAsync(string ruleId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = RuleSelectSql + " WHERE id = $1";
        cmd.Parameters.Add(new DuckDBParameter { Value = ruleId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? MapRule(reader) : null;
    }

    /// <summary>
    ///     Lists alert rules with optional filtering.
    /// </summary>
    public async Task<IReadOnlyList<AlertRuleRow>> ListRulesAsync(
        string? projectId = null,
        string? ruleType = null,
        bool? enabled = null,
        int limit = 100,
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

        if (!string.IsNullOrEmpty(ruleType))
        {
            conditions.Add($"rule_type = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = ruleType });
        }

        if (enabled.HasValue)
        {
            conditions.Add($"enabled = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = enabled.Value });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            {RuleSelectSql}
            {whereClause}
            ORDER BY created_at DESC
            LIMIT ${paramIndex}
            """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 1000) });

        var results = new List<AlertRuleRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapRule(reader));

        return results;
    }

    /// <summary>
    ///     Enables or disables an alert rule.
    /// </summary>
    public async Task<bool> SetRuleEnabledAsync(string ruleId, bool enabled, CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        return await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE alert_rules SET enabled = $1, updated_at = $2 WHERE id = $3";
            cmd.Parameters.Add(new DuckDBParameter { Value = enabled });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = ruleId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes an alert rule by ID.
    /// </summary>
    public async Task<bool> DeleteRuleAsync(string ruleId, CancellationToken ct = default)
    {
        return await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM alert_rules WHERE id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = ruleId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
        }, ct).ConfigureAwait(false);
    }

    // ==========================================================================
    // Alert Firings
    // ==========================================================================

    /// <summary>
    ///     Records a new alert firing.
    /// </summary>
    /// <returns>The firing ID.</returns>
    public async Task<string> RecordFiringAsync(
        string ruleId,
        string fingerprint,
        string severity,
        string title,
        string? message = null,
        double? triggerValue = null,
        double? thresholdValue = null,
        string? contextJson = null,
        CancellationToken ct = default)
    {
        var firingId = Guid.NewGuid().ToString("N");
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO alert_firings
                    (id, rule_id, fingerprint, severity, title, message,
                     trigger_value, threshold_value, context_json, status, fired_at, dedup_key)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, 'firing', $10, $11)
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = firingId });
            cmd.Parameters.Add(new DuckDBParameter { Value = ruleId });
            cmd.Parameters.Add(new DuckDBParameter { Value = fingerprint });
            cmd.Parameters.Add(new DuckDBParameter { Value = severity });
            cmd.Parameters.Add(new DuckDBParameter { Value = title });
            cmd.Parameters.Add(new DuckDBParameter { Value = message ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = triggerValue ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = thresholdValue ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = contextJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = $"{ruleId}:{fingerprint}" });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            // Update trigger count on the rule
            await using var updateCmd = con.CreateCommand();
            updateCmd.CommandText = """
                UPDATE alert_rules SET
                    trigger_count = trigger_count + 1,
                    last_triggered_at = $1,
                    updated_at = $1
                WHERE id = $2
                """;
            updateCmd.Parameters.Add(new DuckDBParameter { Value = now });
            updateCmd.Parameters.Add(new DuckDBParameter { Value = ruleId });
            await updateCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        LogFiringRecorded(firingId, ruleId, severity);
        return firingId;
    }

    /// <summary>
    ///     Lists alert firings with optional filtering.
    /// </summary>
    public async Task<IReadOnlyList<AlertFiringRow>> ListFiringsAsync(
        string? ruleId = null,
        string? status = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        if (!string.IsNullOrEmpty(ruleId))
        {
            conditions.Add($"rule_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = ruleId });
        }

        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add($"status = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = status });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, rule_id, fingerprint, severity, title, message,
                   trigger_value, threshold_value, context_json, status,
                   acknowledged_at, acknowledged_by, resolved_at, fired_at, dedup_key
            FROM alert_firings
            {whereClause}
            ORDER BY fired_at DESC
            LIMIT ${paramIndex}
            """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 1000) });

        var results = new List<AlertFiringRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapFiring(reader));

        return results;
    }

    /// <summary>
    ///     Acknowledges a firing alert.
    /// </summary>
    public async Task<bool> AcknowledgeFiringAsync(string firingId, string acknowledgedBy, CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        return await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                UPDATE alert_firings SET
                    status = 'acknowledged',
                    acknowledged_at = $1,
                    acknowledged_by = $2
                WHERE id = $3 AND status = 'firing'
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = acknowledgedBy });
            cmd.Parameters.Add(new DuckDBParameter { Value = firingId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Resolves a firing or acknowledged alert.
    /// </summary>
    public async Task<bool> ResolveFiringAsync(string firingId, CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        return await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                UPDATE alert_firings SET
                    status = 'resolved',
                    resolved_at = $1
                WHERE id = $2 AND status IN ('firing', 'acknowledged')
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = firingId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
        }, ct).ConfigureAwait(false);
    }

    // ==========================================================================
    // Fix Runs (new migration table schema)
    // ==========================================================================

    /// <summary>
    ///     Creates a fix run record in the new migration-based schema.
    /// </summary>
    public async Task<string> CreateFixRunAsync(
        string issueId,
        string triggerType,
        string strategy,
        string? alertFiringId = null,
        string? modelName = null,
        string? modelProvider = null,
        CancellationToken ct = default)
    {
        var runId = Guid.NewGuid().ToString("N");
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO fix_runs
                    (id, issue_id, alert_firing_id, trigger_type, strategy,
                     model_name, model_provider, status, created_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, 'pending', $8)
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = runId });
            cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            cmd.Parameters.Add(new DuckDBParameter { Value = alertFiringId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = triggerType });
            cmd.Parameters.Add(new DuckDBParameter { Value = strategy });
            cmd.Parameters.Add(new DuckDBParameter { Value = modelName ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = modelProvider ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return runId;
    }

    /// <summary>
    ///     Stores a fix artifact (patch, log, prompt, report) linked to a fix run.
    /// </summary>
    public async Task<string> StoreFixArtifactAsync(
        string fixRunId,
        string artifactType,
        string name,
        string? content = null,
        string contentType = "text/plain",
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        var artifactId = Guid.NewGuid().ToString("N");
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO fix_artifacts
                    (id, fix_run_id, artifact_type, name, content_type,
                     content, size_bytes, metadata_json, created_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = artifactId });
            cmd.Parameters.Add(new DuckDBParameter { Value = fixRunId });
            cmd.Parameters.Add(new DuckDBParameter { Value = artifactType });
            cmd.Parameters.Add(new DuckDBParameter { Value = name });
            cmd.Parameters.Add(new DuckDBParameter { Value = contentType });
            cmd.Parameters.Add(new DuckDBParameter { Value = content ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = content is not null ? (long)content.Length : (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = metadataJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return artifactId;
    }

    // ==========================================================================
    // Private Methods - SQL & Mapping
    // ==========================================================================

    private const string RuleSelectSql = """
        SELECT id, project_id, name, description, rule_type, condition_json,
               threshold_json, target_type, target_filter_json, severity,
               cooldown_seconds, notification_channels_json, enabled,
               last_triggered_at, trigger_count, created_at, updated_at
        FROM alert_rules
        """;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AlertRuleRow MapRule(IDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            ProjectId = reader.GetString(1),
            Name = reader.GetString(2),
            Description = reader.Col(3).AsString,
            RuleType = reader.GetString(4),
            ConditionJson = reader.GetString(5),
            ThresholdJson = reader.Col(6).AsString,
            TargetType = reader.GetString(7),
            TargetFilterJson = reader.Col(8).AsString,
            Severity = reader.GetString(9),
            CooldownSeconds = reader.GetInt32(10),
            NotificationChannelsJson = reader.Col(11).AsString,
            Enabled = reader.GetBoolean(12),
            LastTriggeredAt = reader.Col(13).AsDateTime,
            TriggerCount = reader.GetInt64(14),
            CreatedAt = reader.GetDateTime(15),
            UpdatedAt = reader.GetDateTime(16)
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AlertFiringRow MapFiring(IDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            RuleId = reader.GetString(1),
            Fingerprint = reader.GetString(2),
            Severity = reader.GetString(3),
            Title = reader.GetString(4),
            Message = reader.Col(5).AsString,
            TriggerValue = reader.Col(6).AsDouble,
            ThresholdValue = reader.Col(7).AsDouble,
            ContextJson = reader.Col(8).AsString,
            Status = reader.GetString(9),
            AcknowledgedAt = reader.Col(10).AsDateTime,
            AcknowledgedBy = reader.Col(11).AsString,
            ResolvedAt = reader.Col(12).AsDateTime,
            FiredAt = reader.GetDateTime(13),
            DedupKey = reader.Col(14).AsString
        };

    // ==========================================================================
    // Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Alert rule {RuleId} created: {Name}")]
    private partial void LogRuleCreated(string ruleId, string name);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Alert firing {FiringId} recorded for rule {RuleId} (severity: {Severity})")]
    private partial void LogFiringRecorded(string firingId, string ruleId, string severity);
}

// =============================================================================
// Alert Storage Records
// =============================================================================

/// <summary>
///     Storage row for the <c>alert_rules</c> table.
/// </summary>
public sealed record AlertRuleRow
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string RuleType { get; init; }
    public required string ConditionJson { get; init; }
    public string? ThresholdJson { get; init; }
    public required string TargetType { get; init; }
    public string? TargetFilterJson { get; init; }
    public required string Severity { get; init; }
    public required int CooldownSeconds { get; init; }
    public string? NotificationChannelsJson { get; init; }
    public required bool Enabled { get; init; }
    public DateTime? LastTriggeredAt { get; init; }
    public required long TriggerCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
///     Storage row for the <c>alert_firings</c> table.
/// </summary>
public sealed record AlertFiringRow
{
    public required string Id { get; init; }
    public required string RuleId { get; init; }
    public required string Fingerprint { get; init; }
    public required string Severity { get; init; }
    public required string Title { get; init; }
    public string? Message { get; init; }
    public double? TriggerValue { get; init; }
    public double? ThresholdValue { get; init; }
    public string? ContextJson { get; init; }
    public required string Status { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
    public string? AcknowledgedBy { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public required DateTime FiredAt { get; init; }
    public string? DedupKey { get; init; }
}
