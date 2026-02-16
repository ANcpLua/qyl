-- Selected semconv/features per workspace: configurator state
CREATE TABLE IF NOT EXISTS generation_selections
(
    id
    VARCHAR
    PRIMARY
    KEY,
    workspace_id
    VARCHAR
    NOT
    NULL,
    profile_id
    VARCHAR
    NOT
    NULL,
    selection_type
    VARCHAR
    NOT
    NULL,
    selection_key
    VARCHAR
    NOT
    NULL,
    enabled
    BOOLEAN
    NOT
    NULL
    DEFAULT
    TRUE,
    config_json
    JSON,
    created_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP,
    updated_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP,
    UNIQUE
(
    workspace_id,
    profile_id,
    selection_type,
    selection_key
)
    );

CREATE INDEX IF NOT EXISTS idx_generation_selections_workspace ON generation_selections(workspace_id);
CREATE INDEX IF NOT EXISTS idx_generation_selections_profile ON generation_selections(profile_id);
