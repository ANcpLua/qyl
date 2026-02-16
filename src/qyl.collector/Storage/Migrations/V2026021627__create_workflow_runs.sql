-- Run-level records: top-level workflow execution tracking
CREATE TABLE IF NOT EXISTS workflow_runs
(
    id
    VARCHAR
    PRIMARY
    KEY,
    workflow_id
    VARCHAR
    NOT
    NULL,
    workflow_version
    INTEGER
    NOT
    NULL
    DEFAULT
    1,
    project_id
    VARCHAR
    NOT
    NULL,
    trigger_type
    VARCHAR
    NOT
    NULL,
    trigger_source
    VARCHAR,
    input_json
    JSON,
    output_json
    JSON,
    status
    VARCHAR
    NOT
    NULL
    DEFAULT
    'pending',
    error_message
    TEXT,
    parent_run_id
    VARCHAR,
    correlation_id
    VARCHAR,
    started_at
    TIMESTAMP,
    completed_at
    TIMESTAMP,
    duration_ms
    INTEGER,
    created_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_workflow_runs_workflow ON workflow_runs(workflow_id);
CREATE INDEX IF NOT EXISTS idx_workflow_runs_project ON workflow_runs(project_id);
CREATE INDEX IF NOT EXISTS idx_workflow_runs_status ON workflow_runs(status);
CREATE INDEX IF NOT EXISTS idx_workflow_runs_trigger ON workflow_runs(trigger_type);
CREATE INDEX IF NOT EXISTS idx_workflow_runs_created ON workflow_runs(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_workflow_runs_parent ON workflow_runs(parent_run_id);
CREATE INDEX IF NOT EXISTS idx_workflow_runs_correlation ON workflow_runs(correlation_id);
