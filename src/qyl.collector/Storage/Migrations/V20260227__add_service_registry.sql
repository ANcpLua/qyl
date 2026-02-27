-- Service Registry: Telemetry-derived service auto-detection
-- Design: docs/plans/2026-02-27-service-registry-design.md

CREATE TABLE IF NOT EXISTS service_instances (
    service_namespace        VARCHAR NOT NULL DEFAULT '',
    service_name             VARCHAR NOT NULL,
    service_instance_id      VARCHAR NOT NULL,
    service_type             VARCHAR NOT NULL DEFAULT 'traditional',
    service_version          VARCHAR,
    deployment_environment   VARCHAR,
    os_type                  VARCHAR,
    host_arch                VARCHAR,
    agent_name               VARCHAR,
    provider_name            VARCHAR,
    default_model            VARCHAR,
    first_seen               TIMESTAMP NOT NULL,
    last_seen                TIMESTAMP NOT NULL,
    last_error_at            TIMESTAMP,
    status                   VARCHAR NOT NULL DEFAULT 'active',
    total_spans              BIGINT NOT NULL DEFAULT 0,
    total_logs               BIGINT NOT NULL DEFAULT 0,
    total_errors             BIGINT NOT NULL DEFAULT 0,
    total_input_tokens       BIGINT DEFAULT 0,
    total_output_tokens      BIGINT DEFAULT 0,
    total_cost_usd           DOUBLE DEFAULT 0,
    total_duration_ns        BIGINT DEFAULT 0,
    metadata                 JSON,
    PRIMARY KEY (service_namespace, service_name, service_type, service_instance_id)
);

CREATE OR REPLACE VIEW services AS
SELECT
    service_namespace,
    service_name,
    service_type,
    arg_max(service_version, last_seen) AS latest_version,
    arg_max(provider_name, last_seen) FILTER (WHERE provider_name IS NOT NULL) AS provider_name,
    arg_max(default_model, last_seen) FILTER (WHERE default_model IS NOT NULL) AS default_model,
    MIN(first_seen) AS first_seen,
    MAX(last_seen) AS last_seen,
    MAX(last_error_at) AS last_error_at,
    COUNT(*) AS total_instances,
    COUNT(*) FILTER (WHERE status = 'active') AS active_instances,
    array_agg(DISTINCT deployment_environment) FILTER (WHERE deployment_environment IS NOT NULL) AS environments,
    array_agg(DISTINCT service_version) FILTER (WHERE service_version IS NOT NULL) AS versions_seen,
    SUM(total_spans) AS total_spans,
    SUM(total_logs) AS total_logs,
    SUM(total_errors) AS total_errors,
    SUM(total_input_tokens) AS total_input_tokens,
    SUM(total_output_tokens) AS total_output_tokens,
    SUM(total_cost_usd) AS total_cost_usd,
    SUM(total_duration_ns) AS total_duration_ns,
    SUM(total_duration_ns) / NULLIF(SUM(total_spans), 0) AS avg_duration_ns,
    SUM(total_errors)::DOUBLE / NULLIF(SUM(total_spans) + SUM(total_logs), 0) AS error_rate
FROM service_instances
GROUP BY service_namespace, service_name, service_type;
