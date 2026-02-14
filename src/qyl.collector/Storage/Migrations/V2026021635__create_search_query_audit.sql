-- Query telemetry + relevance: tracks search usage for optimization
CREATE TABLE IF NOT EXISTS search_query_audit (
    id VARCHAR PRIMARY KEY,
    query_text VARCHAR NOT NULL,
    query_type VARCHAR NOT NULL DEFAULT 'text',
    entity_types_json JSON,
    project_id VARCHAR,
    result_count INTEGER NOT NULL DEFAULT 0,
    clicked_result_id VARCHAR,
    clicked_position INTEGER,
    duration_ms INTEGER,
    user_id VARCHAR,
    timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_search_query_audit_query ON search_query_audit(query_text);
CREATE INDEX IF NOT EXISTS idx_search_query_audit_timestamp ON search_query_audit(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_search_query_audit_project ON search_query_audit(project_id);
