// =============================================================================
// Manual schema extensions for workflow execution persistence.
// The base DuckDbSchema.g.cs is auto-generated from TypeSpec; this partial
// adds tables not yet in the TypeSpec model.
// =============================================================================

namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string WorkflowExecutionsDdl = """
        CREATE TABLE IF NOT EXISTS workflow_executions (
            execution_id VARCHAR NOT NULL PRIMARY KEY,
            workflow_name VARCHAR NOT NULL,
            status VARCHAR NOT NULL,
            started_at TIMESTAMP NOT NULL,
            completed_at TIMESTAMP,
            result VARCHAR,
            error VARCHAR,
            parameters_json VARCHAR,
            input_tokens BIGINT,
            output_tokens BIGINT,
            trace_id VARCHAR(32),
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        CREATE INDEX IF NOT EXISTS idx_wf_exec_workflow_name ON workflow_executions(workflow_name);
        CREATE INDEX IF NOT EXISTS idx_wf_exec_status ON workflow_executions(status);
        CREATE INDEX IF NOT EXISTS idx_wf_exec_started_at ON workflow_executions(started_at);
        """;
}
