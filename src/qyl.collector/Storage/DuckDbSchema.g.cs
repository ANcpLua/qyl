// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:18:36.6544370+00:00
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
            deployment.id VARCHAR NOT NULL,
            service.name VARCHAR NOT NULL,
            service.version VARCHAR NOT NULL,
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
            error.type VARCHAR NOT NULL,
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
            session.id VARCHAR(128) NOT NULL,
            user.id VARCHAR,
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
            trace_state VARCHAR,
            name VARCHAR NOT NULL,
            kind DOUBLE NOT NULL,
            start_time_unix_nano BIGINT NOT NULL,
            end_time_unix_nano BIGINT NOT NULL,
            attributes VARCHAR,
            dropped_attributes_count BIGINT,
            events VARCHAR,
            dropped_events_count BIGINT,
            links VARCHAR,
            dropped_links_count BIGINT,
            status VARCHAR NOT NULL,
            flags INTEGER,
            resource VARCHAR NOT NULL,
            instrumentation_scope VARCHAR,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
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
        """;
}
