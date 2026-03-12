-- Loom session state (two-dimensional: stage x status)
CREATE TABLE IF NOT EXISTS loom_sessions (
    session_id       VARCHAR PRIMARY KEY,
    issue_id         VARCHAR NOT NULL,
    mode             VARCHAR NOT NULL DEFAULT 'interactive',
    stage            INTEGER NOT NULL DEFAULT 0,
    stage_name       VARCHAR NOT NULL DEFAULT 'idle',
    status           VARCHAR NOT NULL DEFAULT 'idle',
    created_at       BIGINT  NOT NULL,
    updated_at       BIGINT  NOT NULL,
    root_cause_json  VARCHAR,
    solution_json    VARCHAR,
    fix_run_id       VARCHAR,
    pause_reason     VARCHAR,
    error            VARCHAR,
    metadata_json    VARCHAR
);

CREATE INDEX IF NOT EXISTS idx_loom_sessions_issue
    ON loom_sessions (issue_id);

CREATE INDEX IF NOT EXISTS idx_loom_sessions_mode_status
    ON loom_sessions (mode, status);

CREATE INDEX IF NOT EXISTS idx_loom_sessions_status_stage
    ON loom_sessions (status, stage);

-- Loom message history (persisted for replay protocol)
CREATE TABLE IF NOT EXISTS loom_messages (
    message_id   VARCHAR PRIMARY KEY,
    session_id   VARCHAR NOT NULL,
    role         VARCHAR NOT NULL,
    content      VARCHAR NOT NULL,
    tool_name    VARCHAR,
    tool_args    VARCHAR,
    created_at   BIGINT  NOT NULL,
    sequence     INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_loom_messages_session
    ON loom_messages (session_id, sequence);
