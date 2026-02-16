namespace qyl.collector.Alerting;

/// <summary>
///     DuckDB schema extension for alert history persistence.
/// </summary>
public static class DuckDbSchemaAlerts
{
    public const string AlertHistoryDdl = """
                                          CREATE TABLE IF NOT EXISTS alert_history (
                                              id VARCHAR PRIMARY KEY,
                                              rule_name VARCHAR NOT NULL,
                                              fired_at TIMESTAMP NOT NULL,
                                              resolved_at TIMESTAMP,
                                              query_result DOUBLE,
                                              condition_text VARCHAR,
                                              status VARCHAR NOT NULL,
                                              notification_channels VARCHAR
                                          );
                                          CREATE INDEX IF NOT EXISTS idx_alert_history_rule ON alert_history(rule_name);
                                          CREATE INDEX IF NOT EXISTS idx_alert_history_fired ON alert_history(fired_at);
                                          CREATE INDEX IF NOT EXISTS idx_alert_history_status ON alert_history(status);
                                          """;

    /// <summary>
    ///     Initializes the alert history schema on the given connection.
    /// </summary>
    public static void InitializeAlertSchema(DuckDBConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = AlertHistoryDdl;
        cmd.ExecuteNonQuery();
    }
}
