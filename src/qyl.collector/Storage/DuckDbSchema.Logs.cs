namespace Qyl.Collector.Storage;

public static partial class DuckDbSchema
{
    /// <summary>
    ///     Manual logs DDL with extended columns (log_id, session_id, service_name, source_*).
    ///     Runs before generated schema so CREATE TABLE IF NOT EXISTS in the generated DDL is a no-op.
    /// </summary>
    public const string ManualLogsDdl = """
        CREATE TABLE IF NOT EXISTS logs (
            log_id VARCHAR NOT NULL,
            trace_id VARCHAR,
            span_id VARCHAR,
            session_id VARCHAR,
            time_unix_nano UBIGINT NOT NULL,
            observed_time_unix_nano UBIGINT,
            severity_number TINYINT NOT NULL,
            severity_text VARCHAR,
            body VARCHAR,
            service_name VARCHAR,
            attributes_json VARCHAR,
            resource_json VARCHAR,
            source_file VARCHAR,
            source_line INTEGER,
            source_column INTEGER,
            source_method VARCHAR,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        );
        CREATE INDEX IF NOT EXISTS idx_logs_time ON logs(time_unix_nano);
        CREATE INDEX IF NOT EXISTS idx_logs_service ON logs(service_name);
        CREATE INDEX IF NOT EXISTS idx_logs_severity ON logs(severity_number);
        CREATE INDEX IF NOT EXISTS idx_logs_session ON logs(session_id);
        """;
}
