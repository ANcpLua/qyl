-- Ownership + assignment mapping: code ownership rules for auto-triage
CREATE TABLE IF NOT EXISTS error_ownership (
    id VARCHAR PRIMARY KEY,
    project_id VARCHAR NOT NULL,
    rule_type VARCHAR NOT NULL,
    pattern VARCHAR NOT NULL,
    owner_type VARCHAR NOT NULL,
    owner_id VARCHAR NOT NULL,
    priority INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_error_ownership_project ON error_ownership(project_id);
CREATE INDEX IF NOT EXISTS idx_error_ownership_rule_type ON error_ownership(rule_type);
