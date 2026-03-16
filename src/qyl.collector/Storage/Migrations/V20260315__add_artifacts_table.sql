-- Artifact storage: shareable content produced by AI agent operations.
-- Short-URL retrieval via GET /a/:id.

CREATE TABLE IF NOT EXISTS artifacts (
    id            VARCHAR PRIMARY KEY,
    content_type  VARCHAR NOT NULL DEFAULT 'text/plain',
    content       VARCHAR NOT NULL,
    title         VARCHAR,
    source        VARCHAR,
    metadata_json VARCHAR,
    created_at    TIMESTAMP NOT NULL DEFAULT now(),
    expires_at    TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_artifacts_created ON artifacts(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_artifacts_source  ON artifacts(source);
