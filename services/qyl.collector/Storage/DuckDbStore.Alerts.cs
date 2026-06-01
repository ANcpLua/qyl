
using ANcpLua.OtelConventions.Domains.Alerting;

namespace Qyl.Collector.Storage;

public sealed partial class DuckDbStore
{

    private const string AlertRuleInsertSql = """
                                              INSERT INTO alert_rules (
                                                  id, project_id, name, description, rule_type,
                                                  condition_json, threshold_json, target_type, target_filter_json,
                                                  severity, cooldown_seconds, notification_channels_json, enabled,
                                                  last_triggered_at, trigger_count, created_at, updated_at
                                              ) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17)
                                              """;

    private const string AlertRuleUpdateSql = """
                                              UPDATE alert_rules SET
                                                  project_id = $2,
                                                  name = $3,
                                                  description = $4,
                                                  rule_type = $5,
                                                  condition_json = $6,
                                                  threshold_json = $7,
                                                  target_type = $8,
                                                  target_filter_json = $9,
                                                  severity = $10,
                                                  cooldown_seconds = $11,
                                                  notification_channels_json = $12,
                                                  enabled = $13,
                                                  updated_at = $14
                                              WHERE id = $1
                                              """;

    private const string AlertRuleSelectByIdSql = """
                                                  SELECT id, project_id, name, description, rule_type,
                                                         condition_json, threshold_json, target_type, target_filter_json,
                                                         severity, cooldown_seconds, notification_channels_json, enabled,
                                                         last_triggered_at, trigger_count, created_at, updated_at
                                                  FROM alert_rules
                                                  WHERE id = $1
                                                  """;

    private const string AlertRuleListSql = """
                                            SELECT id, project_id, name, description, rule_type,
                                                   condition_json, threshold_json, target_type, target_filter_json,
                                                   severity, cooldown_seconds, notification_channels_json, enabled,
                                                   last_triggered_at, trigger_count, created_at, updated_at
                                            FROM alert_rules
                                            WHERE ($1 IS NULL OR project_id = $1)
                                              AND ($2 IS NULL OR enabled = $2)
                                            ORDER BY created_at DESC
                                            LIMIT $3
                                            """;


    private const string AlertFiringAcknowledgeSql = """
                                                     UPDATE alert_firings SET
                                                         status = 'acknowledged',
                                                         acknowledged_at = $2,
                                                         acknowledged_by = $3
                                                     WHERE id = $1
                                                     """;

    private const string AlertFiringResolveSql = """
                                                 UPDATE alert_firings SET
                                                     status = 'resolved',
                                                     resolved_at = $2
                                                 WHERE id = $1
                                                 """;

    private const string AlertFiringSelectByIdSql = """
                                                    SELECT id, rule_id, fingerprint, severity, title, message,
                                                           trigger_value, threshold_value, context_json, status,
                                                           acknowledged_at, acknowledged_by, resolved_at, fired_at, dedup_key
                                                    FROM alert_firings
                                                    WHERE id = $1
                                                    """;

    public async Task<AlertRuleEntity> InsertAlertRuleAsync(AlertRuleEntity rule, CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id;
        var toPersist = new AlertRuleEntity
        {
            Id = id,
            ProjectId = rule.ProjectId,
            Name = rule.Name,
            Description = rule.Description,
            RuleType = rule.RuleType,
            ConditionJson = rule.ConditionJson,
            ThresholdJson = rule.ThresholdJson,
            TargetType = rule.TargetType,
            TargetFilterJson = rule.TargetFilterJson,
            Severity = rule.Severity,
            CooldownSeconds = rule.CooldownSeconds is 0 ? 300 : rule.CooldownSeconds,
            NotificationChannelsJson = rule.NotificationChannelsJson,
            Enabled = rule.Enabled,
            LastTriggeredAt = rule.LastTriggeredAt,
            TriggerCount = rule.TriggerCount,
            CreatedAt = rule.CreatedAt == default ? now : rule.CreatedAt,
            UpdatedAt = now
        };

        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = AlertRuleInsertSql;
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.Id });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.ProjectId });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.Name });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)toPersist.Description ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.RuleType.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.ConditionJson });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)toPersist.ThresholdJson ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.TargetType });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)toPersist.TargetFilterJson ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.Severity.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.CooldownSeconds });
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = (object?)toPersist.NotificationChannelsJson ?? DBNull.Value
            });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.Enabled });
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = (object?)toPersist.LastTriggeredAt?.UtcDateTime ?? DBNull.Value
            });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.TriggerCount });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.CreatedAt.UtcDateTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = toPersist.UpdatedAt.UtcDateTime });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return toPersist;
    }

    public async Task<AlertRuleEntity?> UpdateAlertRuleAsync(string ruleId, AlertRuleEntity rule,
        CancellationToken ct = default)
    {
        var existing = await GetAlertRuleAsync(ruleId, ct).ConfigureAwait(false);
        if (existing is null) return null;

        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var rowsAffected = 0;

        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = AlertRuleUpdateSql;
            cmd.Parameters.Add(new DuckDBParameter { Value = ruleId });
            cmd.Parameters.Add(new DuckDBParameter { Value = rule.ProjectId });
            cmd.Parameters.Add(new DuckDBParameter { Value = rule.Name });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)rule.Description ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = rule.RuleType.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = rule.ConditionJson });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)rule.ThresholdJson ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = rule.TargetType });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)rule.TargetFilterJson ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = rule.Severity.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = rule.CooldownSeconds is 0 ? 300 : rule.CooldownSeconds });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)rule.NotificationChannelsJson ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = rule.Enabled });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            rowsAffected = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return rowsAffected > 0 ? await GetAlertRuleAsync(ruleId, ct).ConfigureAwait(false) : null;
    }

    public async Task<bool> DeleteAlertRuleAsync(string ruleId, CancellationToken ct = default)
    {
        var rowsAffected = 0;
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM alert_rules WHERE id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = ruleId });
            rowsAffected = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return rowsAffected > 0;
    }

    public async Task<AlertRuleEntity?> GetAlertRuleAsync(string ruleId, CancellationToken ct = default)
    {
        await using var lease = await GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = AlertRuleSelectByIdSql;
        cmd.Parameters.Add(new DuckDBParameter { Value = ruleId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? MapAlertRule(reader) : null;
    }

    public async Task<IReadOnlyList<AlertRuleEntity>> ListAlertRulesAsync(
        string? projectId, bool? enabled, int limit, CancellationToken ct = default)
    {
        await using var lease = await GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = AlertRuleListSql;
        cmd.Parameters.Add(new DuckDBParameter { Value = (object?)projectId ?? DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = enabled is { } e ? e : DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 100) });

        var results = new List<AlertRuleEntity>(limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(MapAlertRule(reader));
        }

        return results;
    }

    private static AlertRuleEntity MapAlertRule(DbDataReader r) => new()
    {
        Id = r.GetString(0),
        ProjectId = r.GetString(1),
        Name = r.GetString(2),
        Description = r.IsDBNull(3) ? null : r.GetString(3),
        RuleType = ParseEnum<AlertRuleType>(r.GetString(4)),
        ConditionJson = r.GetString(5),
        ThresholdJson = r.IsDBNull(6) ? null : r.GetString(6),
        TargetType = r.GetString(7),
        TargetFilterJson = r.IsDBNull(8) ? null : r.GetString(8),
        Severity = ParseEnum<AlertSeverity>(r.GetString(9)),
        CooldownSeconds = r.GetInt32(10),
        NotificationChannelsJson = r.IsDBNull(11) ? null : r.GetString(11),
        Enabled = r.GetBoolean(12),
        LastTriggeredAt = r.IsDBNull(13) ? null : DateTime.SpecifyKind(r.GetDateTime(13), DateTimeKind.Utc),
        TriggerCount = r.GetInt64(14),
        CreatedAt = DateTime.SpecifyKind(r.GetDateTime(15), DateTimeKind.Utc),
        UpdatedAt = DateTime.SpecifyKind(r.GetDateTime(16), DateTimeKind.Utc)
    };

    private static T ParseEnum<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed) ? parsed : default;

    public async Task<AlertFiringEntity?> AcknowledgeAlertFiringAsync(
        string firingId, string acknowledgedBy, CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var rowsAffected = 0;
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = AlertFiringAcknowledgeSql;
            cmd.Parameters.Add(new DuckDBParameter { Value = firingId });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = acknowledgedBy });
            rowsAffected = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return rowsAffected > 0 ? await GetAlertFiringAsync(firingId, ct).ConfigureAwait(false) : null;
    }

    public async Task<AlertFiringEntity?> ResolveAlertFiringAsync(string firingId, CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var rowsAffected = 0;
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = AlertFiringResolveSql;
            cmd.Parameters.Add(new DuckDBParameter { Value = firingId });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            rowsAffected = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return rowsAffected > 0 ? await GetAlertFiringAsync(firingId, ct).ConfigureAwait(false) : null;
    }

    public async Task<AlertFiringEntity?> GetAlertFiringAsync(string firingId, CancellationToken ct = default)
    {
        await using var lease = await GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = AlertFiringSelectByIdSql;
        cmd.Parameters.Add(new DuckDBParameter { Value = firingId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

        return new AlertFiringEntity
        {
            Id = reader.GetString(0),
            RuleId = reader.GetString(1),
            Fingerprint = reader.GetString(2),
            Severity = ParseEnum<AlertSeverity>(reader.GetString(3)),
            Title = reader.GetString(4),
            Message = reader.IsDBNull(5) ? null : reader.GetString(5),
            TriggerValue = reader.IsDBNull(6) ? null : reader.GetDouble(6),
            ThresholdValue = reader.IsDBNull(7) ? null : reader.GetDouble(7),
            ContextJson = reader.IsDBNull(8) ? null : reader.GetString(8),
            Status = ParseEnum<AlertFiringStatus>(reader.GetString(9)),
            AcknowledgedAt =
                reader.IsDBNull(10) ? null : DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc),
            AcknowledgedBy = reader.IsDBNull(11) ? null : reader.GetString(11),
            ResolvedAt =
                reader.IsDBNull(12) ? null : DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc),
            FiredAt = DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc),
            DedupKey = reader.IsDBNull(14) ? null : reader.GetString(14)
        };
    }
}
