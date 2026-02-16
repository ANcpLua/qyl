-- Job queue for local code generation requests
CREATE TABLE IF NOT EXISTS generation_jobs
(
    id
    VARCHAR
    PRIMARY
    KEY,
    workspace_id
    VARCHAR
    NOT
    NULL,
    profile_id
    VARCHAR
    NOT
    NULL,
    job_type
    VARCHAR
    NOT
    NULL,
    status
    VARCHAR
    NOT
    NULL
    DEFAULT
    'queued',
    priority
    INTEGER
    NOT
    NULL
    DEFAULT
    0,
    input_hash
    VARCHAR,
    output_path
    VARCHAR,
    output_hash
    VARCHAR,
    error_message
    TEXT,
    queued_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP,
    started_at
    TIMESTAMP,
    completed_at
    TIMESTAMP,
    duration_ms
    INTEGER
);

CREATE INDEX IF NOT EXISTS idx_generation_jobs_workspace ON generation_jobs(workspace_id);
CREATE INDEX IF NOT EXISTS idx_generation_jobs_status ON generation_jobs(status);
CREATE INDEX IF NOT EXISTS idx_generation_jobs_queued ON generation_jobs(queued_at DESC);
