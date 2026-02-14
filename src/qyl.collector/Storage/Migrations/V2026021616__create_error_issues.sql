-- Issue aggregates + lifecycle: Sentry-replacement error grouping
CREATE TABLE IF NOT EXISTS error_issues (
    id VARCHAR PRIMARY KEY,
    project_id VARCHAR NOT NULL,
    fingerprint VARCHAR NOT NULL,
    title VARCHAR NOT NULL,
    culprit VARCHAR,
    error_type VARCHAR NOT NULL,
    category VARCHAR NOT NULL DEFAULT 'unknown',
    level VARCHAR NOT NULL DEFAULT 'error',
    platform VARCHAR,
    first_seen_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_seen_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    occurrence_count BIGINT NOT NULL DEFAULT 1,
    affected_users_count INTEGER NOT NULL DEFAULT 0,
    status VARCHAR NOT NULL DEFAULT 'unresolved',
    substatus VARCHAR,
    priority VARCHAR NOT NULL DEFAULT 'medium',
    assigned_to VARCHAR,
    resolved_at TIMESTAMP,
    resolved_by VARCHAR,
    regression_count INTEGER NOT NULL DEFAULT 0,
    last_release VARCHAR,
    tags_json JSON,
    metadata_json JSON,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_error_issues_project ON error_issues(project_id);
CREATE INDEX IF NOT EXISTS idx_error_issues_fingerprint ON error_issues(fingerprint);
CREATE INDEX IF NOT EXISTS idx_error_issues_status ON error_issues(status);
CREATE INDEX IF NOT EXISTS idx_error_issues_priority ON error_issues(priority);
CREATE INDEX IF NOT EXISTS idx_error_issues_first_seen ON error_issues(first_seen_at DESC);
CREATE INDEX IF NOT EXISTS idx_error_issues_last_seen ON error_issues(last_seen_at DESC);
CREATE INDEX IF NOT EXISTS idx_error_issues_assigned ON error_issues(assigned_to);
