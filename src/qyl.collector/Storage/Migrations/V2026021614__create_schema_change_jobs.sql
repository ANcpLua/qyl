-- Schema adaptation DDL jobs: tracks DDL changes from promotions
CREATE TABLE IF NOT EXISTS schema_change_jobs
(
    id
    VARCHAR
    PRIMARY
    KEY,
    promotion_id
    VARCHAR,
    change_type
    VARCHAR
    NOT
    NULL,
    target_table
    VARCHAR
    NOT
    NULL,
    ddl_statement
    TEXT
    NOT
    NULL,
    rollback_ddl
    TEXT,
    status
    VARCHAR
    NOT
    NULL
    DEFAULT
    'pending',
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

CREATE INDEX IF NOT EXISTS idx_schema_change_jobs_status ON schema_change_jobs(status);
CREATE INDEX IF NOT EXISTS idx_schema_change_jobs_table ON schema_change_jobs(target_table);
CREATE INDEX IF NOT EXISTS idx_schema_change_jobs_promotion ON schema_change_jobs(promotion_id);
