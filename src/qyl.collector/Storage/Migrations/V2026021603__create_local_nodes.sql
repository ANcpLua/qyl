-- Local agent/daemon registry: tracks machines running qyl components
CREATE TABLE IF NOT EXISTS local_nodes (
    id VARCHAR PRIMARY KEY,
    project_id VARCHAR NOT NULL,
    environment_id VARCHAR NOT NULL,
    hostname VARCHAR NOT NULL,
    machine_id VARCHAR,
    agent_version VARCHAR,
    os_type VARCHAR,
    os_version VARCHAR,
    first_seen_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_seen_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR NOT NULL DEFAULT 'online'
);

CREATE INDEX IF NOT EXISTS idx_local_nodes_project ON local_nodes(project_id);
CREATE INDEX IF NOT EXISTS idx_local_nodes_status ON local_nodes(status);
CREATE INDEX IF NOT EXISTS idx_local_nodes_last_seen ON local_nodes(last_seen_at DESC);
