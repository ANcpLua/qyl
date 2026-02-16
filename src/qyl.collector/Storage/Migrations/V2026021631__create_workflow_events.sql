-- Append-only event stream + cursor: workflow event sourcing
CREATE TABLE IF NOT EXISTS workflow_events
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
    VARCHAR,
    event_type
    VARCHAR
    NOT
    NULL,
    event_name
    VARCHAR
    NOT
    NULL,
    payload_json
    JSON,
    sequence_number
    BIGINT
    NOT
    NULL,
    source
    VARCHAR,
    correlation_id
    VARCHAR,
    timestamp
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_workflow_events_run ON workflow_events(run_id);
CREATE INDEX IF NOT EXISTS idx_workflow_events_node ON workflow_events(run_id, node_id);
CREATE INDEX IF NOT EXISTS idx_workflow_events_type ON workflow_events(event_type);
CREATE INDEX IF NOT EXISTS idx_workflow_events_seq ON workflow_events(run_id, sequence_number);
CREATE INDEX IF NOT EXISTS idx_workflow_events_timestamp ON workflow_events(timestamp DESC);
