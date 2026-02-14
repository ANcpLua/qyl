-- Browser-local handshake sessions: PKCE-style workspace verification
CREATE TABLE IF NOT EXISTS handshake_sessions (
    id VARCHAR PRIMARY KEY,
    workspace_id VARCHAR NOT NULL,
    challenge VARCHAR NOT NULL,
    challenge_method VARCHAR NOT NULL DEFAULT 'S256',
    browser_fingerprint VARCHAR,
    origin_url VARCHAR,
    state VARCHAR NOT NULL DEFAULT 'pending',
    verified_at TIMESTAMP,
    expires_at TIMESTAMP NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_handshake_sessions_workspace ON handshake_sessions(workspace_id);
CREATE INDEX IF NOT EXISTS idx_handshake_sessions_state ON handshake_sessions(state);
CREATE INDEX IF NOT EXISTS idx_handshake_sessions_expires ON handshake_sessions(expires_at);
