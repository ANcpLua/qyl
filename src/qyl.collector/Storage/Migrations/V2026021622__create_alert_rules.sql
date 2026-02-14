-- Rule definitions + conditions: alerting engine configuration
CREATE TABLE IF NOT EXISTS alert_rules (
    id VARCHAR PRIMARY KEY,
    project_id VARCHAR NOT NULL,
    name VARCHAR NOT NULL,
    description TEXT,
    rule_type VARCHAR NOT NULL,
    condition_json JSON NOT NULL,
    threshold_json JSON,
    target_type VARCHAR NOT NULL,
    target_filter_json JSON,
    severity VARCHAR NOT NULL DEFAULT 'warning',
    cooldown_seconds INTEGER NOT NULL DEFAULT 300,
    notification_channels_json JSON,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    last_triggered_at TIMESTAMP,
    trigger_count BIGINT NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_alert_rules_project ON alert_rules(project_id);
CREATE INDEX IF NOT EXISTS idx_alert_rules_type ON alert_rules(rule_type);
CREATE INDEX IF NOT EXISTS idx_alert_rules_enabled ON alert_rules(enabled);
CREATE INDEX IF NOT EXISTS idx_alert_rules_severity ON alert_rules(severity);
