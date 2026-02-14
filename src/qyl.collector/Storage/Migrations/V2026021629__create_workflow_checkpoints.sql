-- Durable node snapshots: enables workflow resumption after crash
CREATE TABLE IF NOT EXISTS workflow_checkpoints (
    id VARCHAR PRIMARY KEY,
    run_id VARCHAR NOT NULL,
    node_id VARCHAR NOT NULL,
    checkpoint_type VARCHAR NOT NULL,
    state_json JSON NOT NULL,
    sequence_number BIGINT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (run_id, node_id, sequence_number)
);

CREATE INDEX IF NOT EXISTS idx_workflow_checkpoints_run ON workflow_checkpoints(run_id);
CREATE INDEX IF NOT EXISTS idx_workflow_checkpoints_node ON workflow_checkpoints(run_id, node_id);
CREATE INDEX IF NOT EXISTS idx_workflow_checkpoints_seq ON workflow_checkpoints(run_id, sequence_number DESC);
