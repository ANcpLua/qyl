-- Versioned state KV per run: cross-node shared data with optimistic concurrency
CREATE TABLE IF NOT EXISTS workflow_shared_state (
    run_id VARCHAR NOT NULL,
    key VARCHAR NOT NULL,
    value_json JSON NOT NULL,
    version INTEGER NOT NULL DEFAULT 1,
    updated_by VARCHAR,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (run_id, key)
);

CREATE INDEX IF NOT EXISTS idx_workflow_shared_state_run ON workflow_shared_state(run_id);
