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
            "genai_cost_drift",
            $"GenAI cost exceeds {thresholdPercent}% increase over 24h baseline",
            """
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
            $"> {thresholdPercent.ToString(CultureInfo.InvariantCulture)}",
            TimeSpan.FromSeconds(intervalSeconds),
            TimeSpan.FromMinutes(30),
            [new NotificationChannel("console", null)]);

    /// <summary>
    ///     Alert when total tokens consumed exceed a configured limit within a time window.
    /// </summary>
    public static AlertRule TokenBudgetExceeded(long tokenLimit = 1_000_000, int intervalSeconds = 60) =>
        new(
            "genai_token_budget_exceeded",
            $"GenAI total tokens exceed {tokenLimit:N0} in the last hour",
            """
            SELECT COALESCE(SUM(
                CAST(json_extract(attributes, '$.gen_ai.usage.input_tokens') AS BIGINT) +
                CAST(json_extract(attributes, '$.gen_ai.usage.output_tokens') AS BIGINT)
            ), 0)
            FROM spans
            WHERE start_time > now() - INTERVAL 1 HOUR
            AND json_extract_string(attributes, '$.gen_ai.operation.name') IS NOT NULL
            """,
            $"> {tokenLimit.ToString(CultureInfo.InvariantCulture)}",
            TimeSpan.FromSeconds(intervalSeconds),
            TimeSpan.FromMinutes(15),
            [new NotificationChannel("console", null)]);

    /// <summary>
    ///     Alert when the error rate for any GenAI model exceeds a threshold percentage.
    /// </summary>
    public static AlertRule ModelDegradation(double errorRatePercent = 10, int intervalSeconds = 120) =>
        new(
            "genai_model_degradation",
            $"GenAI model error rate exceeds {errorRatePercent}%",
            """
            SELECT COALESCE(
                CAST(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS DOUBLE) /
                NULLIF(CAST(COUNT(*) AS DOUBLE), 0) * 100.0
            , 0)
            FROM spans
            WHERE start_time > now() - INTERVAL 1 HOUR
            AND json_extract_string(attributes, '$.gen_ai.operation.name') IS NOT NULL
            """,
            $"> {errorRatePercent.ToString(CultureInfo.InvariantCulture)}",
            TimeSpan.FromSeconds(intervalSeconds),
            TimeSpan.FromMinutes(15),
            [new NotificationChannel("console", null)]);

    /// <summary>
    ///     Alert when an agent run duration exceeds a threshold in seconds.
    /// </summary>
    public static AlertRule AgentTimeout(double thresholdSeconds = 300, int intervalSeconds = 60) =>
        new(
            "genai_agent_timeout",
            $"GenAI agent run duration exceeds {thresholdSeconds}s",
            """
            SELECT COALESCE(MAX(
                EXTRACT(EPOCH FROM (end_time - start_time))
            ), 0)
            FROM spans
            WHERE start_time > now() - INTERVAL 10 MINUTE
            AND (json_extract_string(attributes, '$.gen_ai.operation.name') = 'create_agent'
              OR json_extract_string(attributes, '$.gen_ai.operation.name') = 'invoke_agent')
            """,
            $"> {thresholdSeconds.ToString(CultureInfo.InvariantCulture)}",
            TimeSpan.FromSeconds(intervalSeconds),
            TimeSpan.FromMinutes(10),
            [new NotificationChannel("console", null)]);

    /// <summary>
    ///     Returns all built-in GenAI alert rules with default thresholds.
    /// </summary>
    public static IReadOnlyList<AlertRule> GetDefaultRules() =>
        [CostDrift(), TokenBudgetExceeded(), ModelDegradation(), AgentTimeout()];
}
