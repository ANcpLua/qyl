-- Local process identity + ports: tracks running qyl processes per workspace
CREATE TABLE IF NOT EXISTS workspace_process_registry
(
    id
    VARCHAR
    PRIMARY
    KEY,
    workspace_id
    VARCHAR
    NOT
    NULL,
    process_type
    VARCHAR
    NOT
    NULL,
    pid
    INTEGER
    NOT
    NULL,
    port
    INTEGER,
    protocol
    VARCHAR,
    binary_path
    VARCHAR,
    binary_version
    VARCHAR,
    started_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP,
    last_heartbeat_at
    TIMESTAMP,
    status
    VARCHAR
    NOT
    NULL
    DEFAULT
    'running'
);

CREATE INDEX IF NOT EXISTS idx_workspace_process_registry_workspace ON workspace_process_registry(workspace_id);
CREATE INDEX IF NOT EXISTS idx_workspace_process_registry_type ON workspace_process_registry(process_type);
CREATE INDEX IF NOT EXISTS idx_workspace_process_registry_status ON workspace_process_registry(status);
