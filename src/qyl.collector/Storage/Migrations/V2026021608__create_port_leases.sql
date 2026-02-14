-- Collision-proof port leases: prevents port conflicts across workspaces
CREATE TABLE IF NOT EXISTS port_leases (
    port INTEGER NOT NULL,
    workspace_id VARCHAR NOT NULL,
    process_id VARCHAR NOT NULL,
    protocol VARCHAR NOT NULL DEFAULT 'tcp',
    leased_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP NOT NULL,
    released_at TIMESTAMP,
    PRIMARY KEY (port, protocol)
);

CREATE INDEX IF NOT EXISTS idx_port_leases_workspace ON port_leases(workspace_id);
CREATE INDEX IF NOT EXISTS idx_port_leases_process ON port_leases(process_id);
CREATE INDEX IF NOT EXISTS idx_port_leases_expires ON port_leases(expires_at);
