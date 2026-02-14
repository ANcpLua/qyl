-- Workspace envelope: the local-first workspace unit with heartbeat tracking
CREATE TABLE IF NOT EXISTS workspace_envelopes (
    id VARCHAR PRIMARY KEY,
    project_id VARCHAR NOT NULL,
    environment_id VARCHAR NOT NULL,
    node_id VARCHAR NOT NULL,
    name VARCHAR NOT NULL,
    root_path VARCHAR NOT NULL,
    heartbeat_at TIMESTAMP,
    heartbeat_interval_seconds INTEGER NOT NULL DEFAULT 30,
    status VARCHAR NOT NULL DEFAULT 'active',
    config_json JSON,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_workspace_envelopes_project ON workspace_envelopes(project_id);
CREATE INDEX IF NOT EXISTS idx_workspace_envelopes_node ON workspace_envelopes(node_id);
CREATE INDEX IF NOT EXISTS idx_workspace_envelopes_status ON workspace_envelopes(status);
CREATE INDEX IF NOT EXISTS idx_workspace_envelopes_heartbeat ON workspace_envelopes(heartbeat_at DESC);
