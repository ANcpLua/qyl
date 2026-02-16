-- Promoted columns from profiles: tracks attribute-to-column promotions
CREATE TABLE IF NOT EXISTS schema_promotions
(
    id
    VARCHAR
    PRIMARY
    KEY,
    profile_id
    VARCHAR
    NOT
    NULL,
    source_attribute
    VARCHAR
    NOT
    NULL,
    target_column
    VARCHAR
    NOT
    NULL,
    target_type
    VARCHAR
    NOT
    NULL,
    target_table
    VARCHAR
    NOT
    NULL,
    index_type
    VARCHAR,
    status
    VARCHAR
    NOT
    NULL
    DEFAULT
    'pending',
    applied_at
    TIMESTAMP,
    created_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP,
    UNIQUE
(
    target_table,
    target_column
)
    );

CREATE INDEX IF NOT EXISTS idx_schema_promotions_profile ON schema_promotions(profile_id);
CREATE INDEX IF NOT EXISTS idx_schema_promotions_table ON schema_promotions(target_table);
CREATE INDEX IF NOT EXISTS idx_schema_promotions_status ON schema_promotions(status);
