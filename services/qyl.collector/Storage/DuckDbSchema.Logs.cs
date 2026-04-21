namespace Qyl.Collector.Storage;

public static partial class DuckDbSchema
{
    /// <summary>
    ///     Manual logs DDL merging the generated schema columns with the extended columns
    ///     added by migration V20260214 (log_id, session_id, service_name, source_*).
    ///     Runs before generated schema so CREATE TABLE IF NOT EXISTS in the generated DDL is a no-op.
    ///     Includes original columns (resource, attributes, etc.) so migrations can ALTER them.
    ///     Indexes are deferred to migration files to avoid DuckDB ALTER TABLE conflicts.
    /// </summary>
    public const string ManualLogsDdl = """
                                        CREATE TABLE IF NOT EXISTS logs (
                                            log_id VARCHAR,
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
                                            attributes VARCHAR,
                                            dropped_attributes_count BIGINT,
                                            flags INTEGER,
                                            resource VARCHAR,
                                            instrumentation_scope VARCHAR,
                                            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                                        );
                                        """;
}
