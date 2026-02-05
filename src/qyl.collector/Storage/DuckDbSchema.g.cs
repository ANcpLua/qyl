// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T05:57:09.6168730+00:00
//     DuckDB schema definitions
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace qyl.collector.Storage;

/// <summary>DuckDB schema from TypeSpec God Schema.</summary>
public static partial class DuckDbSchema
{
    public const int Version = 20260123;

    public const string DeploymentsDdl = """
        CREATE TABLE IF NOT EXISTS deployments (
            deployment_id VARCHAR NOT NULL,
            service_name VARCHAR NOT NULL,
            service_version VARCHAR NOT NULL,
            environment VARCHAR NOT NULL,
            status VARCHAR NOT NULL,
            strategy VARCHAR NOT NULL,
            start_time TIMESTAMP NOT NULL,
            end_time TIMESTAMP,
            duration_s DOUBLE,
            deployed_by VARCHAR,
            git_commit VARCHAR,
            git_branch VARCHAR,
            previous_version VARCHAR,
            rollback_target VARCHAR,
            replica_count INTEGER,
            healthy_replicas INTEGER,
            error_message VARCHAR,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        """;

    public const string ErrorsDdl = """
        CREATE TABLE IF NOT EXISTS errors (
            error_id VARCHAR NOT NULL,
            error_type VARCHAR NOT NULL,
            message VARCHAR NOT NULL,
            category VARCHAR NOT NULL,
            fingerprint VARCHAR NOT NULL,
            first_seen TIMESTAMP NOT NULL,
            last_seen TIMESTAMP NOT NULL,
            occurrence_count BIGINT NOT NULL,
            affected_users BIGINT,
            affected_services VARCHAR,
            status VARCHAR NOT NULL,
            assigned_to VARCHAR,
            issue_url VARCHAR,
            sample_traces VARCHAR,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        """;

    public const string LogsDdl = """
        CREATE TABLE IF NOT EXISTS logs (
            time_unix_nano BIGINT NOT NULL,
            observed_time_unix_nano BIGINT NOT NULL,
            severity_number DOUBLE NOT NULL,
            severity_text VARCHAR,
            body VARCHAR NOT NULL,
            attributes VARCHAR,
            dropped_attributes_count BIGINT,
            flags INTEGER,
            trace_id VARCHAR(32),
            span_id VARCHAR(16),
            resource VARCHAR NOT NULL,
            instrumentation_scope VARCHAR,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        """;

    public const string SessionEntitiesDdl = """
        CREATE TABLE IF NOT EXISTS session_entities (
            session_id VARCHAR(128) NOT NULL,
            user_id VARCHAR,
            start_time TIMESTAMP NOT NULL,
            end_time TIMESTAMP,
            duration_ms DOUBLE,
            trace_count INTEGER NOT NULL,
            span_count INTEGER NOT NULL,
            error_count INTEGER NOT NULL,
            state VARCHAR NOT NULL,
            client VARCHAR,
            geo VARCHAR,
            genai_usage VARCHAR,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        """;

    public const string SpansDdl = """
        CREATE TABLE IF NOT EXISTS spans (
            span_id VARCHAR(16) NOT NULL,
            trace_id VARCHAR(32) NOT NULL,
            parent_span_id VARCHAR(16),
            session_id VARCHAR(128),
            name VARCHAR NOT NULL,
            kind DOUBLE NOT NULL,
            start_time_unix_nano BIGINT NOT NULL,
            end_time_unix_nano BIGINT NOT NULL,
            duration_ns UBIGINT NOT NULL,
            status_code DOUBLE NOT NULL,
            status_message VARCHAR,
            service_name VARCHAR,
            gen_ai_provider_name VARCHAR,
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
            baggage_json VARCHAR,
            schema_url VARCHAR(256),
            created_at TIMESTAMP DEFAULT now(),
            PRIMARY KEY (span_id)
        );
        """;

    public static string GetSchemaDdl() =>
        $"""
        -- QYL DuckDB Schema v20260123
        {DeploymentsDdl}
        {ErrorsDdl}
        {LogsDdl}
        {SessionEntitiesDdl}
        {SpansDdl}
        CREATE INDEX IF NOT EXISTS idx_spans_trace_id ON spans(trace_id);
        CREATE INDEX IF NOT EXISTS idx_spans_session_id ON spans(session_id);
        CREATE INDEX IF NOT EXISTS idx_spans_start_time ON spans(start_time_unix_nano);
        CREATE INDEX IF NOT EXISTS idx_spans_service_name ON spans(service_name);
        CREATE INDEX IF NOT EXISTS idx_spans_gen_ai_provider_name ON spans(gen_ai_provider_name);
        CREATE INDEX IF NOT EXISTS idx_spans_gen_ai_request_model ON spans(gen_ai_request_model);
        """;
}
