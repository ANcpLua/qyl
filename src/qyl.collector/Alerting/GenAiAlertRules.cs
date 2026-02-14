namespace qyl.collector.Alerting;

/// <summary>
///     Built-in GenAI-specific alert rule templates.
///     These generate SQL queries against the spans table using OTel GenAI semantic conventions.
/// </summary>
public static class GenAiAlertRules
{
    /// <summary>
    ///     Alert when GenAI cost exceeds a percentage increase over the rolling baseline.
    ///     Compares last hour's cost to the average hourly cost over the previous 24 hours.
    /// </summary>
    public static AlertRule CostDrift(double thresholdPercent = 50, int intervalSeconds = 300) =>
        new(
            Name: "genai_cost_drift",
            Description: $"GenAI cost exceeds {thresholdPercent}% increase over 24h baseline",
            Query: """
                WITH recent AS (
                    SELECT COALESCE(SUM(
                        (CAST(json_extract(attributes, '$.gen_ai.usage.input_tokens') AS DOUBLE) / 1000000.0) * 2.5 +
                        (CAST(json_extract(attributes, '$.gen_ai.usage.output_tokens') AS DOUBLE) / 1000000.0) * 10.0
                    ), 0) AS cost
                    FROM spans
                    WHERE start_time > now() - INTERVAL 1 HOUR
                    AND json_extract_string(attributes, '$.gen_ai.operation.name') IS NOT NULL
                ),
                baseline AS (
                    SELECT COALESCE(SUM(
                        (CAST(json_extract(attributes, '$.gen_ai.usage.input_tokens') AS DOUBLE) / 1000000.0) * 2.5 +
                        (CAST(json_extract(attributes, '$.gen_ai.usage.output_tokens') AS DOUBLE) / 1000000.0) * 10.0
                    ) / 24.0, 0.01) AS hourly_avg
                    FROM spans
                    WHERE start_time > now() - INTERVAL 25 HOUR
                    AND start_time <= now() - INTERVAL 1 HOUR
                    AND json_extract_string(attributes, '$.gen_ai.operation.name') IS NOT NULL
                )
                SELECT CASE WHEN baseline.hourly_avg > 0
                    THEN ((recent.cost - baseline.hourly_avg) / baseline.hourly_avg) * 100.0
                    ELSE 0 END
                FROM recent, baseline
                """,
            Condition: $"> {thresholdPercent.ToString(CultureInfo.InvariantCulture)}",
            Interval: TimeSpan.FromSeconds(intervalSeconds),
            Cooldown: TimeSpan.FromMinutes(30),
            Channels: [new NotificationChannel("console", null)]);

    /// <summary>
    ///     Alert when total tokens consumed exceed a configured limit within a time window.
    /// </summary>
    public static AlertRule TokenBudgetExceeded(long tokenLimit = 1_000_000, int intervalSeconds = 60) =>
        new(
            Name: "genai_token_budget_exceeded",
            Description: $"GenAI total tokens exceed {tokenLimit:N0} in the last hour",
            Query: $"""
                SELECT COALESCE(SUM(
                    CAST(json_extract(attributes, '$.gen_ai.usage.input_tokens') AS BIGINT) +
                    CAST(json_extract(attributes, '$.gen_ai.usage.output_tokens') AS BIGINT)
                ), 0)
                FROM spans
                WHERE start_time > now() - INTERVAL 1 HOUR
                AND json_extract_string(attributes, '$.gen_ai.operation.name') IS NOT NULL
                """,
            Condition: $"> {tokenLimit.ToString(CultureInfo.InvariantCulture)}",
            Interval: TimeSpan.FromSeconds(intervalSeconds),
            Cooldown: TimeSpan.FromMinutes(15),
            Channels: [new NotificationChannel("console", null)]);

    /// <summary>
    ///     Alert when the error rate for any GenAI model exceeds a threshold percentage.
    /// </summary>
    public static AlertRule ModelDegradation(double errorRatePercent = 10, int intervalSeconds = 120) =>
        new(
            Name: "genai_model_degradation",
            Description: $"GenAI model error rate exceeds {errorRatePercent}%",
            Query: $"""
                SELECT COALESCE(
                    CAST(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS DOUBLE) /
                    NULLIF(CAST(COUNT(*) AS DOUBLE), 0) * 100.0
                , 0)
                FROM spans
                WHERE start_time > now() - INTERVAL 1 HOUR
                AND json_extract_string(attributes, '$.gen_ai.operation.name') IS NOT NULL
                """,
            Condition: $"> {errorRatePercent.ToString(CultureInfo.InvariantCulture)}",
            Interval: TimeSpan.FromSeconds(intervalSeconds),
            Cooldown: TimeSpan.FromMinutes(15),
            Channels: [new NotificationChannel("console", null)]);

    /// <summary>
    ///     Alert when an agent run duration exceeds a threshold in seconds.
    /// </summary>
    public static AlertRule AgentTimeout(double thresholdSeconds = 300, int intervalSeconds = 60) =>
        new(
            Name: "genai_agent_timeout",
            Description: $"GenAI agent run duration exceeds {thresholdSeconds}s",
            Query: $"""
                SELECT COALESCE(MAX(
                    EXTRACT(EPOCH FROM (end_time - start_time))
                ), 0)
                FROM spans
                WHERE start_time > now() - INTERVAL 10 MINUTE
                AND (json_extract_string(attributes, '$.gen_ai.operation.name') = 'create_agent'
                  OR json_extract_string(attributes, '$.gen_ai.operation.name') = 'invoke_agent')
                """,
            Condition: $"> {thresholdSeconds.ToString(CultureInfo.InvariantCulture)}",
            Interval: TimeSpan.FromSeconds(intervalSeconds),
            Cooldown: TimeSpan.FromMinutes(10),
            Channels: [new NotificationChannel("console", null)]);

    /// <summary>
    ///     Returns all built-in GenAI alert rules with default thresholds.
    /// </summary>
    public static IReadOnlyList<AlertRule> GetDefaultRules() =>
        [CostDrift(), TokenBudgetExceeded(), ModelDegradation(), AgentTimeout()];
}
