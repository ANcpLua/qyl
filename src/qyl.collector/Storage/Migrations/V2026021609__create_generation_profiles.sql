-- Named instrumentation profiles: reusable codegen configurations
CREATE TABLE IF NOT EXISTS generation_profiles (
    id VARCHAR PRIMARY KEY,
    project_id VARCHAR NOT NULL,
    name VARCHAR NOT NULL,
    description TEXT,
    target_framework VARCHAR NOT NULL,
    target_language VARCHAR NOT NULL DEFAULT 'csharp',
    semconv_version VARCHAR NOT NULL DEFAULT '1.39.0',
    features_json JSON NOT NULL,
    template_overrides_json JSON,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_generation_profiles_project ON generation_profiles(project_id);
CREATE INDEX IF NOT EXISTS idx_generation_profiles_default ON generation_profiles(project_id, is_default);
