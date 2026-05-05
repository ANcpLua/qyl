namespace Qyl.Collector.Storage;

public static partial class DuckDbSchema
{
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
