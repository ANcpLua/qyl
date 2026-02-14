-- Autofix run ledger: tracks AI-assisted fix attempts
CREATE TABLE IF NOT EXISTS fix_runs (
    id VARCHAR PRIMARY KEY,
    issue_id VARCHAR NOT NULL,
    alert_firing_id VARCHAR,
    trigger_type VARCHAR NOT NULL,
    strategy VARCHAR NOT NULL,
    model_name VARCHAR,
    model_provider VARCHAR,
    status VARCHAR NOT NULL DEFAULT 'pending',
    error_message TEXT,
    tokens_used INTEGER,
    duration_ms INTEGER,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    started_at TIMESTAMP,
    completed_at TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_fix_runs_issue ON fix_runs(issue_id);
CREATE INDEX IF NOT EXISTS idx_fix_runs_status ON fix_runs(status);
CREATE INDEX IF NOT EXISTS idx_fix_runs_trigger ON fix_runs(trigger_type);
CREATE INDEX IF NOT EXISTS idx_fix_runs_created ON fix_runs(created_at DESC);
