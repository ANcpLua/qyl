-- Patches, logs, reports, prompts: fix run outputs
CREATE TABLE IF NOT EXISTS fix_artifacts
(
    id
    VARCHAR
    PRIMARY
    KEY,
    fix_run_id
    VARCHAR
    NOT
    NULL,
    artifact_type
    VARCHAR
    NOT
    NULL,
    name
    VARCHAR
    NOT
    NULL,
    content_type
    VARCHAR
    NOT
    NULL
    DEFAULT
    'text/plain',
    content
    TEXT,
    content_hash
    VARCHAR,
    size_bytes
    BIGINT,
    metadata_json
    JSON,
    created_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_fix_artifacts_run ON fix_artifacts(fix_run_id);
CREATE INDEX IF NOT EXISTS idx_fix_artifacts_type ON fix_artifacts(artifact_type);
