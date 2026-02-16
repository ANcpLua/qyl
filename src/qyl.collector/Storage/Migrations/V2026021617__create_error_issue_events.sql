-- Event-level records linked to issues: individual error occurrences
CREATE TABLE IF NOT EXISTS error_issue_events
(
    id
    VARCHAR
    PRIMARY
    KEY,
    issue_id
    VARCHAR
    NOT
    NULL,
    trace_id
    VARCHAR,
    span_id
    VARCHAR,
    message
    TEXT,
    stack_trace
    TEXT,
    stack_frames_json
    JSON,
    environment
    VARCHAR,
    release_version
    VARCHAR,
    user_id
    VARCHAR,
    user_ip
    VARCHAR,
    request_url
    VARCHAR,
    request_method
    VARCHAR,
    browser
    VARCHAR,
    os
    VARCHAR,
    device
    VARCHAR,
    runtime
    VARCHAR,
    runtime_version
    VARCHAR,
    context_json
    JSON,
    tags_json
    JSON,
    timestamp
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_error_issue_events_issue ON error_issue_events(issue_id);
CREATE INDEX IF NOT EXISTS idx_error_issue_events_trace ON error_issue_events(trace_id);
CREATE INDEX IF NOT EXISTS idx_error_issue_events_timestamp ON error_issue_events(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_error_issue_events_release ON error_issue_events(release_version);
CREATE INDEX IF NOT EXISTS idx_error_issue_events_user ON error_issue_events(user_id);
