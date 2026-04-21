-- Pre-error context timeline: ambient activity before crash
CREATE TABLE IF NOT EXISTS error_breadcrumbs
(
    id
    VARCHAR
    PRIMARY
    KEY,
    event_id
    VARCHAR
    NOT
    NULL,
    breadcrumb_type
    VARCHAR
    NOT
    NULL,
    category
    VARCHAR,
    message
    TEXT,
    level
    VARCHAR
    NOT
    NULL
    DEFAULT
    'info',
    data_json
    JSON,
    timestamp
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_error_breadcrumbs_event ON error_breadcrumbs(event_id);
CREATE INDEX IF NOT EXISTS idx_error_breadcrumbs_type ON error_breadcrumbs(breadcrumb_type);
CREATE INDEX IF NOT EXISTS idx_error_breadcrumbs_timestamp ON error_breadcrumbs(timestamp DESC);
