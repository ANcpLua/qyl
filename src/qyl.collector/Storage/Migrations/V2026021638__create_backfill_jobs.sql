-- Backfill/reindex/repair jobs: tracks bulk data maintenance operations
CREATE TABLE IF NOT EXISTS backfill_jobs (
    id VARCHAR PRIMARY KEY,
    job_type VARCHAR NOT NULL,
    target_table VARCHAR NOT NULL,
    description TEXT,
    filter_json JSON,
    status VARCHAR NOT NULL DEFAULT 'pending',
    error_message TEXT,
    total_rows BIGINT,
    processed_rows BIGINT NOT NULL DEFAULT 0,
    progress_pct DOUBLE NOT NULL DEFAULT 0.0,
    queued_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    duration_ms INTEGER
);

CREATE INDEX IF NOT EXISTS idx_backfill_jobs_status ON backfill_jobs(status);
CREATE INDEX IF NOT EXISTS idx_backfill_jobs_table ON backfill_jobs(target_table);
CREATE INDEX IF NOT EXISTS idx_backfill_jobs_type ON backfill_jobs(job_type);
CREATE INDEX IF NOT EXISTS idx_backfill_jobs_queued ON backfill_jobs(queued_at DESC);
