-- Conditional branch predicates: DAG routing configuration
CREATE TABLE IF NOT EXISTS workflow_routing_rules
(
    id
    VARCHAR
    PRIMARY
    KEY,
    workflow_id
    VARCHAR
    NOT
    NULL,
    source_node_id
    VARCHAR
    NOT
    NULL,
    target_node_id
    VARCHAR
    NOT
    NULL,
    condition_type
    VARCHAR
    NOT
    NULL,
    condition_expression
    TEXT
    NOT
    NULL,
    priority
    INTEGER
    NOT
    NULL
    DEFAULT
    0,
    is_default
    BOOLEAN
    NOT
    NULL
    DEFAULT
    FALSE,
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

CREATE INDEX IF NOT EXISTS idx_workflow_routing_rules_workflow ON workflow_routing_rules(workflow_id);
CREATE INDEX IF NOT EXISTS idx_workflow_routing_rules_source ON workflow_routing_rules(source_node_id);
