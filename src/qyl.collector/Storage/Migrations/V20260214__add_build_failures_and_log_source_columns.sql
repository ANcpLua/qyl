CREATE TABLE IF NOT EXISTS build_failures
(
    id
    VARCHAR
    PRIMARY
    KEY,
    timestamp
    TIMESTAMP
    NOT
    NULL,
    target
    VARCHAR
    NOT
    NULL,
    exit_code
    INTEGER
    NOT
    NULL,
    binlog_path
    VARCHAR,
    error_summary
    TEXT,
    property_issues_json
    JSON,
    env_reads_json
    JSON,
    call_stack_json
    JSON,
    duration_ms
    INTEGER,
    created_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_build_failures_timestamp ON build_failures(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_build_failures_target ON build_failures(target);

ALTER TABLE logs
    ADD COLUMN IF NOT EXISTS source_file VARCHAR;
ALTER TABLE logs
    ADD COLUMN IF NOT EXISTS source_line INTEGER;
ALTER TABLE logs
    ADD COLUMN IF NOT EXISTS source_column INTEGER;
ALTER TABLE logs
    ADD COLUMN IF NOT EXISTS source_method VARCHAR;
ALTER TABLE logs
    ADD COLUMN IF NOT EXISTS log_id VARCHAR;
ALTER TABLE logs
    ADD COLUMN IF NOT EXISTS session_id VARCHAR;
ALTER TABLE logs
    ADD COLUMN IF NOT EXISTS service_name VARCHAR;
ALTER TABLE logs
    ADD COLUMN IF NOT EXISTS attributes_json VARCHAR;
ALTER TABLE logs
    ADD COLUMN IF NOT EXISTS resource_json VARCHAR;

ALTER TABLE logs
    ALTER COLUMN observed_time_unix_nano DROP NOT NULL;
ALTER TABLE logs
    ALTER COLUMN resource DROP NOT NULL;

UPDATE logs
SET log_id = REPLACE(CAST(uuid() AS VARCHAR), '-', '')
WHERE log_id IS NULL;

CREATE INDEX IF NOT EXISTS idx_logs_source_file ON logs(source_file);
