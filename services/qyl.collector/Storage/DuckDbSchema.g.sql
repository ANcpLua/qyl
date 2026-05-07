-- QYL DuckDB Schema v901717775
CREATE TABLE IF NOT EXISTS alert_rules (
    id VARCHAR NOT NULL,
    project_id VARCHAR NOT NULL,
    name VARCHAR NOT NULL,
    description VARCHAR,
    rule_type VARCHAR NOT NULL,
    condition_json JSON NOT NULL,
    threshold_json JSON,
    target_type VARCHAR NOT NULL,
    target_filter_json JSON,
    severity VARCHAR NOT NULL,
    cooldown_seconds INTEGER NOT NULL,
    notification_channels_json JSON,
    enabled BOOLEAN NOT NULL,
    last_triggered_at TIMESTAMP,
    trigger_count BIGINT NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);
CREATE TABLE IF NOT EXISTS alert_firings (
    id VARCHAR NOT NULL,
    rule_id VARCHAR NOT NULL,
    fingerprint VARCHAR NOT NULL,
    severity VARCHAR NOT NULL,
    title VARCHAR NOT NULL,
    message VARCHAR,
    trigger_value DOUBLE,
    threshold_value DOUBLE,
    context_json JSON,
    status VARCHAR NOT NULL,
    acknowledged_at TIMESTAMP,
    acknowledged_by VARCHAR,
    resolved_at TIMESTAMP,
    fired_at TIMESTAMP NOT NULL,
    dedup_key VARCHAR,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS fix_runs (
    id VARCHAR NOT NULL,
    issue_id VARCHAR NOT NULL,
    alert_firing_id VARCHAR,
    trigger_type VARCHAR NOT NULL,
    strategy VARCHAR NOT NULL,
    model_name VARCHAR,
    model_provider VARCHAR,
    status VARCHAR NOT NULL,
    error_message VARCHAR,
    tokens_used INTEGER,
    duration_ms INTEGER,
    created_at TIMESTAMP NOT NULL,
    started_at TIMESTAMP,
    completed_at TIMESTAMP
);
CREATE TABLE IF NOT EXISTS generation_profiles (
    id VARCHAR NOT NULL,
    project_id VARCHAR NOT NULL,
    name VARCHAR NOT NULL,
    description VARCHAR,
    target_framework VARCHAR NOT NULL,
    target_language VARCHAR NOT NULL,
    semconv_version VARCHAR NOT NULL,
    features_json JSON NOT NULL,
    template_overrides_json JSON,
    is_default BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);
CREATE TABLE IF NOT EXISTS generation_selections (
    id VARCHAR NOT NULL,
    workspace_id VARCHAR NOT NULL,
    profile_id VARCHAR NOT NULL,
    selection_type VARCHAR NOT NULL,
    selection_key VARCHAR NOT NULL,
    enabled BOOLEAN NOT NULL,
    config_json JSON,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);
CREATE TABLE IF NOT EXISTS generation_jobs (
    id VARCHAR NOT NULL,
    workspace_id VARCHAR NOT NULL,
    profile_id VARCHAR NOT NULL,
    job_type VARCHAR NOT NULL,
    status VARCHAR NOT NULL,
    priority INTEGER NOT NULL,
    input_hash VARCHAR,
    output_path VARCHAR,
    output_hash VARCHAR,
    error_message VARCHAR,
    queued_at TIMESTAMP NOT NULL,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    duration_ms INTEGER,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS error_issues (
    id VARCHAR NOT NULL,
    project_id VARCHAR NOT NULL,
    fingerprint VARCHAR NOT NULL,
    title VARCHAR NOT NULL,
    culprit VARCHAR,
    error_type VARCHAR NOT NULL,
    category VARCHAR NOT NULL,
    level VARCHAR NOT NULL,
    platform VARCHAR,
    first_seen_at TIMESTAMP NOT NULL,
    last_seen_at TIMESTAMP NOT NULL,
    occurrence_count BIGINT NOT NULL,
    affected_users_count INTEGER NOT NULL,
    status VARCHAR NOT NULL,
    substatus VARCHAR,
    priority VARCHAR NOT NULL,
    assigned_to VARCHAR,
    resolved_at TIMESTAMP,
    resolved_by VARCHAR,
    regression_count INTEGER NOT NULL,
    last_release VARCHAR,
    tags_json JSON,
    metadata_json JSON,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);
CREATE TABLE IF NOT EXISTS error_issue_events (
    id VARCHAR NOT NULL,
    issue_id VARCHAR NOT NULL,
    trace_id VARCHAR,
    span_id VARCHAR,
    message VARCHAR,
    stack_trace VARCHAR,
    stack_frames_json JSON,
    environment VARCHAR,
    release_version VARCHAR,
    user_id VARCHAR,
    user_ip VARCHAR,
    request_url VARCHAR,
    request_method VARCHAR,
    browser VARCHAR,
    os VARCHAR,
    device VARCHAR,
    runtime VARCHAR,
    runtime_version VARCHAR,
    context_json JSON,
    tags_json JSON,
    timestamp TIMESTAMP NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS error_breadcrumbs (
    id VARCHAR NOT NULL,
    event_id VARCHAR NOT NULL,
    breadcrumb_type VARCHAR NOT NULL,
    category VARCHAR,
    message VARCHAR,
    level VARCHAR NOT NULL,
    data_json JSON,
    timestamp TIMESTAMP NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
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
CREATE TABLE IF NOT EXISTS workflow_runs (
    id VARCHAR NOT NULL,
    workflow_id VARCHAR NOT NULL,
    workflow_version INTEGER NOT NULL,
    project_id VARCHAR NOT NULL,
    trigger_type VARCHAR NOT NULL,
    trigger_source VARCHAR,
    input_json JSON,
    output_json JSON,
    status VARCHAR NOT NULL,
    error_message VARCHAR,
    parent_run_id VARCHAR,
    correlation_id VARCHAR,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    duration_ms INTEGER,
    created_at TIMESTAMP NOT NULL
);
CREATE TABLE IF NOT EXISTS workflow_nodes (
    id VARCHAR NOT NULL,
    run_id VARCHAR NOT NULL,
    node_id VARCHAR NOT NULL,
    node_type VARCHAR NOT NULL,
    node_name VARCHAR NOT NULL,
    attempt INTEGER NOT NULL,
    input_json JSON,
    output_json JSON,
    status VARCHAR NOT NULL,
    error_message VARCHAR,
    retry_count INTEGER NOT NULL,
    max_retries INTEGER NOT NULL,
    timeout_ms INTEGER,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    duration_ms INTEGER,
    created_at TIMESTAMP NOT NULL
);
CREATE TABLE IF NOT EXISTS workflow_events (
    id VARCHAR NOT NULL,
    run_id VARCHAR NOT NULL,
    node_id VARCHAR,
    event_type VARCHAR NOT NULL,
    event_name VARCHAR NOT NULL,
    payload_json JSON,
    sequence_number BIGINT NOT NULL,
    source VARCHAR,
    correlation_id VARCHAR,
    timestamp TIMESTAMP NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS projects (
    id VARCHAR NOT NULL,
    name VARCHAR NOT NULL,
    slug VARCHAR NOT NULL,
    description VARCHAR,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL,
    archived_at TIMESTAMP
);
CREATE TABLE IF NOT EXISTS project_environments (
    id VARCHAR NOT NULL,
    project_id VARCHAR NOT NULL,
    name VARCHAR NOT NULL,
    display_name VARCHAR NOT NULL,
    color VARCHAR,
    sort_order INTEGER NOT NULL,
    created_at TIMESTAMP NOT NULL
);
CREATE TABLE IF NOT EXISTS workspace_envelopes (
    id VARCHAR NOT NULL,
    project_id VARCHAR NOT NULL,
    environment_id VARCHAR NOT NULL,
    node_id VARCHAR NOT NULL,
    name VARCHAR NOT NULL,
    root_path VARCHAR NOT NULL,
    heartbeat_at TIMESTAMP,
    heartbeat_interval_seconds INTEGER NOT NULL,
    status VARCHAR NOT NULL,
    config_json JSON,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);
CREATE TABLE IF NOT EXISTS handshake_sessions (
    id VARCHAR NOT NULL,
    workspace_id VARCHAR NOT NULL,
    challenge VARCHAR NOT NULL,
    challenge_method VARCHAR NOT NULL,
    browser_fingerprint VARCHAR,
    origin_url VARCHAR,
    state VARCHAR NOT NULL,
    verified_at TIMESTAMP,
    expires_at TIMESTAMP NOT NULL,
    created_at TIMESTAMP NOT NULL
);
CREATE TABLE IF NOT EXISTS session_entities (
    session_id VARCHAR NOT NULL,
    user_id VARCHAR,
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP,
    duration_ms DOUBLE,
    trace_count INTEGER NOT NULL,
    span_count INTEGER NOT NULL,
    error_count INTEGER NOT NULL,
    services VARCHAR[] NOT NULL,
    state VARCHAR NOT NULL,
    client JSON,
    geo JSON,
    genai_usage JSON,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
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
    affected_services VARCHAR[],
    status VARCHAR NOT NULL,
    assigned_to VARCHAR,
    issue_url VARCHAR,
    sample_traces VARCHAR[],
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS spans (
    span_id VARCHAR NOT NULL,
    PRIMARY KEY (span_id),
    trace_id VARCHAR NOT NULL,
    parent_span_id VARCHAR,
    session_id VARCHAR,
    name VARCHAR NOT NULL,
    kind VARCHAR NOT NULL,
    start_time_unix_nano BIGINT NOT NULL,
    end_time_unix_nano BIGINT NOT NULL,
    duration_ns BIGINT NOT NULL,
    status_code VARCHAR NOT NULL,
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
    created_at TIMESTAMP DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_spans_trace_id ON spans(trace_id);
CREATE INDEX IF NOT EXISTS idx_spans_session_id ON spans(session_id);
CREATE INDEX IF NOT EXISTS idx_spans_start_time ON spans(start_time_unix_nano);
CREATE INDEX IF NOT EXISTS idx_spans_service_name ON spans(service_name);
CREATE INDEX IF NOT EXISTS idx_spans_gen_ai_provider_name ON spans(gen_ai_provider_name);
CREATE INDEX IF NOT EXISTS idx_spans_gen_ai_request_model ON spans(gen_ai_request_model);
CREATE TABLE IF NOT EXISTS profiles (
    profile_id VARCHAR NOT NULL,
    PRIMARY KEY (profile_id),
    trace_id VARCHAR,
    span_id VARCHAR,
    session_id VARCHAR,
    time_unix_nano BIGINT NOT NULL,
    duration_nano BIGINT NOT NULL,
    sample_count INTEGER NOT NULL,
    sample_type VARCHAR,
    sample_unit VARCHAR,
    original_payload_format VARCHAR,
    service_name VARCHAR,
    profile_frame_type VARCHAR,
    attributes_json VARCHAR,
    resource_json VARCHAR,
    profile_data_json VARCHAR,
    schema_url VARCHAR(256),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_profiles_trace_id ON profiles(trace_id);
CREATE INDEX IF NOT EXISTS idx_profiles_session_id ON profiles(session_id);
CREATE INDEX IF NOT EXISTS idx_profiles_time ON profiles(time_unix_nano);
CREATE INDEX IF NOT EXISTS idx_profiles_sample_type ON profiles(sample_type);
CREATE INDEX IF NOT EXISTS idx_profiles_service ON profiles(service_name);
