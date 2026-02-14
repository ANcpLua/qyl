-- Environment rows per project (dev, staging, prod, custom)
CREATE TABLE IF NOT EXISTS project_environments (
    id VARCHAR PRIMARY KEY,
    project_id VARCHAR NOT NULL,
    name VARCHAR NOT NULL,
    display_name VARCHAR NOT NULL,
    color VARCHAR,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (project_id, name)
);

CREATE INDEX IF NOT EXISTS idx_project_environments_project ON project_environments(project_id);
