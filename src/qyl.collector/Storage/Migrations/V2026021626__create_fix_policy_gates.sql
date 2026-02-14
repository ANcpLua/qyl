-- Guardrails + policy decisions: autofix safety gates
CREATE TABLE IF NOT EXISTS fix_policy_gates (
    id VARCHAR PRIMARY KEY,
    fix_run_id VARCHAR NOT NULL,
    gate_type VARCHAR NOT NULL,
    gate_name VARCHAR NOT NULL,
    decision VARCHAR NOT NULL,
    reason TEXT,
    input_json JSON,
    output_json JSON,
    evaluated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_fix_policy_gates_run ON fix_policy_gates(fix_run_id);
CREATE INDEX IF NOT EXISTS idx_fix_policy_gates_type ON fix_policy_gates(gate_type);
CREATE INDEX IF NOT EXISTS idx_fix_policy_gates_decision ON fix_policy_gates(decision);
