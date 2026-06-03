namespace Qyl.Collector.Storage;

internal static partial class DuckDbSchema
{
    public const string SpansDdl = """
                                   CREATE TABLE IF NOT EXISTS spans (
                                       span_id VARCHAR NOT NULL,
                                       trace_id VARCHAR NOT NULL,
                                       PRIMARY KEY (trace_id, span_id),
                                       parent_span_id VARCHAR,
                                       session_id VARCHAR,
                                       name VARCHAR NOT NULL,
                                       kind TINYINT NOT NULL,
                                       start_time_unix_nano UBIGINT NOT NULL,
                                       end_time_unix_nano UBIGINT NOT NULL,
                                       duration_ns UBIGINT NOT NULL,
                                       status_code TINYINT NOT NULL,
                                       service_name VARCHAR,
                                       gen_ai_provider_name VARCHAR,
                                       gen_ai_request_model VARCHAR,
                                       gen_ai_response_model VARCHAR,
                                       gen_ai_input_tokens BIGINT,
                                       gen_ai_output_tokens BIGINT,
                                       gen_ai_temperature DOUBLE,
                                       gen_ai_stop_reason VARCHAR,
                                       gen_ai_tool_name VARCHAR,
                                       gen_ai_cost_usd DOUBLE,
                                       attributes_json VARCHAR,
                                       resource_json VARCHAR,
                                       schema_url VARCHAR(256),
                                       created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                                   );
                                   """;

    public const string CoreIndexesDdl = """
                                         CREATE INDEX IF NOT EXISTS idx_spans_trace_id ON spans(trace_id);
                                         CREATE INDEX IF NOT EXISTS idx_spans_session_id ON spans(session_id);
                                         CREATE INDEX IF NOT EXISTS idx_spans_start_time ON spans(start_time_unix_nano);
                                         CREATE INDEX IF NOT EXISTS idx_spans_service_name ON spans(service_name);
                                         CREATE INDEX IF NOT EXISTS idx_spans_gen_ai_provider_name ON spans(gen_ai_provider_name);
                                         CREATE INDEX IF NOT EXISTS idx_spans_gen_ai_request_model ON spans(gen_ai_request_model);
                                         """;
}
