namespace Qyl.Collector.Storage;

internal static partial class DuckDbSchema
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
                                            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                                        );
                                        """;
}
