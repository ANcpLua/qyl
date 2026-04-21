-- Canonical search docs: unified search index across all entity types
CREATE TABLE IF NOT EXISTS search_documents
(
    id
    VARCHAR
    PRIMARY
    KEY,
    entity_type
    VARCHAR
    NOT
    NULL,
    entity_id
    VARCHAR
    NOT
    NULL,
    project_id
    VARCHAR,
    title
    VARCHAR
    NOT
    NULL,
    body
    TEXT,
    url
    VARCHAR,
    tags_json
    JSON,
    metadata_json
    JSON,
    boost
    DOUBLE
    NOT
    NULL
    DEFAULT
    1.0,
    indexed_at
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
    entity_type,
    entity_id
)
    );

CREATE INDEX IF NOT EXISTS idx_search_documents_entity_type ON search_documents(entity_type);
CREATE INDEX IF NOT EXISTS idx_search_documents_project ON search_documents(project_id);
CREATE INDEX IF NOT EXISTS idx_search_documents_updated ON search_documents(updated_at DESC);
