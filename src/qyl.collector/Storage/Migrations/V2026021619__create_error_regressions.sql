-- Regression windows across releases: tracks when resolved issues recur
CREATE TABLE IF NOT EXISTS error_regressions (
    id VARCHAR PRIMARY KEY,
    issue_id VARCHAR NOT NULL,
    resolved_in_release VARCHAR NOT NULL,
    regressed_in_release VARCHAR NOT NULL,
    resolved_at TIMESTAMP NOT NULL,
    regressed_at TIMESTAMP NOT NULL,
    occurrence_count_before INTEGER NOT NULL DEFAULT 0,
    occurrence_count_after INTEGER NOT NULL DEFAULT 0,
    auto_detected BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_error_regressions_issue ON error_regressions(issue_id);
CREATE INDEX IF NOT EXISTS idx_error_regressions_regressed ON error_regressions(regressed_at DESC);
CREATE INDEX IF NOT EXISTS idx_error_regressions_release ON error_regressions(regressed_in_release);
