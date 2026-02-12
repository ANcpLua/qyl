namespace qyl.collector.Alerting;

/// <summary>
///     Minimal API endpoints for the alerting subsystem.
/// </summary>
public static class AlertEndpoints
{
    internal static IEndpointRouteBuilder MapAlertEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/alerts")
            .WithTags("Alerts");

        group.MapGet("/", GetAlertRules)
            .WithName("GetAlertRules")
            .WithSummary("List all configured alert rules and their current state");

        group.MapGet("/history", GetAlertHistory)
            .WithName("GetAlertHistory")
            .WithSummary("Get alert history from DuckDB");

        group.MapGet("/active", GetActiveAlerts)
            .WithName("GetActiveAlerts")
            .WithSummary("Get currently firing alerts");

        return endpoints;
    }

    private static IResult GetAlertRules(
        AlertConfigLoader configLoader,
        AlertEvaluator evaluator)
    {
        var config = configLoader.Current;
        var states = evaluator.States;

        var rules = config.Alerts.Select(r =>
        {
            states.TryGetValue(r.Name, out var state);
            return new
            {
                r.Name,
                r.Description,
                r.Query,
                r.Condition,
                IntervalSeconds = (int)r.Interval.TotalSeconds,
                CooldownSeconds = (int)r.Cooldown.TotalSeconds,
                Channels = r.Channels.Select(c => new { c.Type, c.Url }),
                State = state is null
                    ? null
                    : new
                    {
                        state.IsFiring,
                        state.LastFiredAt,
                        state.LastEvaluatedAt,
                        state.LastQueryResult
                    }
            };
        });

        return Results.Ok(rules);
    }

    private static async Task<IResult> GetAlertHistory(
        DuckDbStore store,
        string? ruleName = null,
        string? status = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        if (!string.IsNullOrEmpty(ruleName))
        {
            conditions.Add($"rule_name = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = ruleName });
        }

        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add($"status = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = status });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        cmd.CommandText = $"""
            SELECT id, rule_name, fired_at, resolved_at, query_result, condition_text, status, notification_channels
            FROM alert_history
            {whereClause}
            ORDER BY fired_at DESC
            LIMIT ${paramIndex}
            """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var history = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            history.Add(new
            {
                Id = reader.GetString(0),
                RuleName = reader.GetString(1),
                FiredAt = new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero),
                ResolvedAt = reader.Col(3).AsDateTimeOffset,
                QueryResult = reader.Col(4).AsDouble,
                ConditionText = reader.Col(5).AsString,
                Status = reader.GetString(6),
                NotificationChannels = reader.Col(7).AsString
            });
        }

        return Results.Ok(history);
    }

    private static IResult GetActiveAlerts(AlertEvaluator evaluator)
    {
        var active = evaluator.States
            .Where(static kvp => kvp.Value.IsFiring)
            .Select(static kvp => new
            {
                kvp.Value.RuleName,
                kvp.Value.IsFiring,
                kvp.Value.LastFiredAt,
                kvp.Value.LastEvaluatedAt,
                kvp.Value.LastQueryResult,
                kvp.Value.ActiveAlertId
            });

        return Results.Ok(active);
    }
}
