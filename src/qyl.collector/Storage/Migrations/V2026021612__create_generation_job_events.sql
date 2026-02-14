-- Job lifecycle event stream: append-only audit trail for generation jobs
CREATE TABLE IF NOT EXISTS generation_job_events (
    id VARCHAR PRIMARY KEY,
    job_id VARCHAR NOT NULL,
    event_type VARCHAR NOT NULL,
    message TEXT,
    details_json JSON,
    timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_generation_job_events_job ON generation_job_events(job_id);
CREATE INDEX IF NOT EXISTS idx_generation_job_events_type ON generation_job_events(event_type);
CREATE INDEX IF NOT EXISTS idx_generation_job_events_timestamp ON generation_job_events(timestamp DESC);
