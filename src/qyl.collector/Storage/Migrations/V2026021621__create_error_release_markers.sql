-- Deploy markers for auto-transition: correlate releases with issue state
CREATE TABLE IF NOT EXISTS error_release_markers
(
    id
    VARCHAR
    PRIMARY
    KEY,
    project_id
    VARCHAR
    NOT
    NULL,
    release_version
    VARCHAR
    NOT
    NULL,
    environment
    VARCHAR
    NOT
    NULL,
    commit_sha
    VARCHAR,
    commit_message
    TEXT,
    deployed_by
    VARCHAR,
    issues_resolved_count
    INTEGER
    NOT
    NULL
    DEFAULT
    0,
    issues_regressed_count
    INTEGER
    NOT
    NULL
    DEFAULT
    0,
    deployed_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP,
    created_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP,
    UNIQUE
(
    project_id,
    release_version,
    environment
)
    );

CREATE INDEX IF NOT EXISTS idx_error_release_markers_project ON error_release_markers(project_id);
CREATE INDEX IF NOT EXISTS idx_error_release_markers_release ON error_release_markers(release_version);
CREATE INDEX IF NOT EXISTS idx_error_release_markers_deployed ON error_release_markers(deployed_at DESC);
