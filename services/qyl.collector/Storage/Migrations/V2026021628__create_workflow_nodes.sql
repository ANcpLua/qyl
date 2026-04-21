-- Node executions + attempts: individual DAG node tracking
CREATE TABLE IF NOT EXISTS workflow_nodes
(
    id
    VARCHAR
    PRIMARY
    KEY,
    run_id
    VARCHAR
    NOT
    NULL,
    node_id
    VARCHAR
    NOT
    NULL,
    node_type
    VARCHAR
    NOT
    NULL,
    node_name
    VARCHAR
    NOT
    NULL,
    attempt
    INTEGER
    NOT
    NULL
    DEFAULT
    1,
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
    retry_count
    INTEGER
    NOT
    NULL
    DEFAULT
    0,
    max_retries
    INTEGER
    NOT
    NULL
    DEFAULT
    3,
    timeout_ms
    INTEGER,
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

CREATE INDEX IF NOT EXISTS idx_workflow_nodes_run ON workflow_nodes(run_id);
CREATE INDEX IF NOT EXISTS idx_workflow_nodes_node ON workflow_nodes(node_id);
CREATE INDEX IF NOT EXISTS idx_workflow_nodes_status ON workflow_nodes(status);
CREATE INDEX IF NOT EXISTS idx_workflow_nodes_type ON workflow_nodes(node_type);
