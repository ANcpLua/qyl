-- Compaction checkpoints + HWM: tracks DuckDB compaction progress
CREATE TABLE IF NOT EXISTS compaction_state
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
    high_watermark
    TIMESTAMP,
    low_watermark
    TIMESTAMP,
    rows_compacted
    BIGINT
    NOT
    NULL
    DEFAULT
    0,
    rows_remaining
    BIGINT
    NOT
    NULL
    DEFAULT
    0,
    last_compaction_at
    TIMESTAMP,
    last_duration_ms
    INTEGER,
    next_scheduled_at
    TIMESTAMP,
    status
    VARCHAR
    NOT
    NULL
    DEFAULT
    'idle',
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

CREATE INDEX IF NOT EXISTS idx_compaction_state_table ON compaction_state(target_table);
CREATE INDEX IF NOT EXISTS idx_compaction_state_status ON compaction_state(status);
