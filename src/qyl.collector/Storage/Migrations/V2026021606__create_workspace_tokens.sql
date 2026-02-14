-- Token metadata + revocation: workspace-scoped API tokens
CREATE TABLE IF NOT EXISTS workspace_tokens (
    id VARCHAR PRIMARY KEY,
    workspace_id VARCHAR NOT NULL,
    name VARCHAR NOT NULL,
    token_hash VARCHAR NOT NULL,
    token_prefix VARCHAR NOT NULL,
    scopes_json JSON,
    last_used_at TIMESTAMP,
    expires_at TIMESTAMP,
    revoked_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_workspace_tokens_workspace ON workspace_tokens(workspace_id);
CREATE INDEX IF NOT EXISTS idx_workspace_tokens_hash ON workspace_tokens(token_hash);
CREATE INDEX IF NOT EXISTS idx_workspace_tokens_prefix ON workspace_tokens(token_prefix);
