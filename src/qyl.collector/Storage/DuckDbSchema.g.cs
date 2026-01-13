// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    schema/generated/openapi.yaml
//     Generated: 2026-01-13T06:07:10.6805820+00:00
//     DuckDB schema definitions
// =============================================================================
// To modify: update TypeSpec in schema/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace qyl.collector.Storage;

/// <summary>DuckDB schema from TypeSpec God Schema.</summary>
public static partial class DuckDbSchema
{
    public const int Version = 20260113;

    public const string SessionsDdl = """
        CREATE TABLE IF NOT EXISTS sessions (
            session_id VARCHAR(32) NOT NULL,
            start_time UBIGINT NOT NULL,
            end_time UBIGINT NOT NULL,
            span_count BIGINT NOT NULL,
            error_count BIGINT NOT NULL,
            total_input_tokens BIGINT NOT NULL,
            total_output_tokens BIGINT NOT NULL,
            total_cost_usd DOUBLE NOT NULL,
            service_name VARCHAR,
            gen_ai_system VARCHAR,
            gen_ai_model VARCHAR,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            PRIMARY KEY (session_id)
        );
        """;

    public const string SpansDdl = """
        CREATE TABLE IF NOT EXISTS spans (
            span_id VARCHAR(16) NOT NULL,
            trace_id VARCHAR(32) NOT NULL,
            parent_span_id VARCHAR(16),
            session_id VARCHAR(32),
            name VARCHAR NOT NULL,
            kind TINYINT NOT NULL,
            start_time_unix_nano UBIGINT NOT NULL,
            end_time_unix_nano UBIGINT NOT NULL,
            duration_ns UBIGINT NOT NULL,
            status_code TINYINT NOT NULL,
            status_message VARCHAR,
            service_name VARCHAR,
            gen_ai_system VARCHAR,
            gen_ai_request_model VARCHAR,
            gen_ai_response_model VARCHAR,
            gen_ai_input_tokens BIGINT,
            gen_ai_output_tokens BIGINT,
            gen_ai_temperature DOUBLE,
            gen_ai_stop_reason VARCHAR,
            gen_ai_tool_name VARCHAR,
            gen_ai_tool_call_id VARCHAR,
            gen_ai_cost_usd DOUBLE,
            attributes_json VARCHAR,
            resource_json VARCHAR,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            PRIMARY KEY (span_id)
        );
        """;

    public static string GetSchemaDdl() =>
        $"""
        -- QYL DuckDB Schema v20260113
        {SessionsDdl}
        {SpansDdl}
        CREATE INDEX IF NOT EXISTS idx_spans_trace_id ON spans(trace_id);
        CREATE INDEX IF NOT EXISTS idx_spans_session_id ON spans(session_id);
        CREATE INDEX IF NOT EXISTS idx_spans_start_time ON spans(start_time_unix_nano);
        CREATE INDEX IF NOT EXISTS idx_spans_service_name ON spans(service_name);
        """;
}
