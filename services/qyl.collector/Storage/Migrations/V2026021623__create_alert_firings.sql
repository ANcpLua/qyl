-- Triggered alert history + dedupe: immutable audit trail of fired alerts
CREATE TABLE IF NOT EXISTS alert_firings
(
    id
    VARCHAR
    PRIMARY
    KEY,
    rule_id
    VARCHAR
    NOT
    NULL,
    fingerprint
    VARCHAR
    NOT
    NULL,
    severity
    VARCHAR
    NOT
    NULL,
    title
    VARCHAR
    NOT
    NULL,
    message
    TEXT,
    trigger_value
    DOUBLE,
    threshold_value
    DOUBLE,
    context_json
    JSON,
    status
    VARCHAR
    NOT
    NULL
    DEFAULT
    'firing',
    acknowledged_at
    TIMESTAMP,
    acknowledged_by
    VARCHAR,
    resolved_at
    TIMESTAMP,
    fired_at
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    CURRENT_TIMESTAMP,
    dedup_key
    VARCHAR
);

CREATE INDEX IF NOT EXISTS idx_alert_firings_rule ON alert_firings(rule_id);
CREATE INDEX IF NOT EXISTS idx_alert_firings_status ON alert_firings(status);
CREATE INDEX IF NOT EXISTS idx_alert_firings_fired ON alert_firings(fired_at DESC);
CREATE INDEX IF NOT EXISTS idx_alert_firings_fingerprint ON alert_firings(fingerprint);
CREATE INDEX IF NOT EXISTS idx_alert_firings_dedup ON alert_firings(dedup_key);
