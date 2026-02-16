-- Table retention rules: configurable per-table data lifecycle
CREATE TABLE IF NOT EXISTS retention_policies
(
    id
    VARCHAR
    PRIMARY
    KEY,
    target_table
    VARCHAR
    NOT
    NULL
    UNIQUE,
    retention_days
    INTEGER
    NOT
    NULL,
    max_row_count
    BIGINT,
    cleanup_strategy
    VARCHAR
    NOT
    NULL
    DEFAULT
    'delete',
    last_cleanup_at
    TIMESTAMP,
    rows_cleaned
    BIGINT
    NOT
    NULL
    DEFAULT
    0,
    enabled
    BOOLEAN
    NOT
    NULL
    DEFAULT
    TRUE,
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
    CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_retention_policies_table ON retention_policies(target_table);
CREATE INDEX IF NOT EXISTS idx_retention_policies_enabled ON retention_policies(enabled);
