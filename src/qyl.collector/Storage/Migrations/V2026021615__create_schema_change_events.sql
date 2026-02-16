-- DDL execution audit trail: append-only log for schema change jobs
CREATE TABLE IF NOT EXISTS schema_change_events
(
    id
    VARCHAR
    PRIMARY
    KEY,
    job_id
    VARCHAR
    NOT
    NULL,
    event_type
    VARCHAR
    NOT
    NULL,
    message
    TEXT,
    details_json
    JSON,
    rows_affected
    BIGINT,
    timestamp
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_schema_change_events_job ON schema_change_events(job_id);
CREATE INDEX IF NOT EXISTS idx_schema_change_events_type ON schema_change_events(event_type);
CREATE INDEX IF NOT EXISTS idx_schema_change_events_timestamp ON schema_change_events(timestamp DESC);
