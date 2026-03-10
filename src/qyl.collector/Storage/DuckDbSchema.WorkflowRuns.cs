namespace Qyl.Collector.Storage;

public static partial class DuckDbSchema
{
    public const string WorkflowRunsV2Ddl = """
                                            CREATE TABLE IF NOT EXISTS workflow_runs (
                                                id VARCHAR PRIMARY KEY,
                                                workflow_id VARCHAR NOT NULL,
                                                workflow_version INTEGER NOT NULL DEFAULT 1,
                                                project_id VARCHAR NOT NULL,
                                                trigger_type VARCHAR NOT NULL,
                                                trigger_source VARCHAR,
                                                input_json JSON,
                                                output_json JSON,
                                                status VARCHAR NOT NULL DEFAULT 'pending',
                                                error_message TEXT,
                                                parent_run_id VARCHAR,
                                                correlation_id VARCHAR,
                                                started_at TIMESTAMP,
                                                completed_at TIMESTAMP,
                                                duration_ms INTEGER,
                                                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                                            );
                                            CREATE INDEX IF NOT EXISTS idx_workflow_runs_workflow ON workflow_runs(workflow_id);
                                            CREATE INDEX IF NOT EXISTS idx_workflow_runs_project ON workflow_runs(project_id);
                                            CREATE INDEX IF NOT EXISTS idx_workflow_runs_status ON workflow_runs(status);
                                            CREATE INDEX IF NOT EXISTS idx_workflow_runs_trigger ON workflow_runs(trigger_type);
                                            CREATE INDEX IF NOT EXISTS idx_workflow_runs_created ON workflow_runs(created_at DESC);
                                            CREATE INDEX IF NOT EXISTS idx_workflow_runs_parent ON workflow_runs(parent_run_id);
                                            CREATE INDEX IF NOT EXISTS idx_workflow_runs_correlation ON workflow_runs(correlation_id);
                                            """;

    public const string WorkflowNodesV2Ddl = """
                                             CREATE TABLE IF NOT EXISTS workflow_nodes (
                                                 id VARCHAR PRIMARY KEY,
                                                 run_id VARCHAR NOT NULL,
                                                 node_id VARCHAR NOT NULL,
                                                 node_type VARCHAR NOT NULL,
                                                 node_name VARCHAR NOT NULL,
                                                 attempt INTEGER NOT NULL DEFAULT 1,
                                                 input_json JSON,
                                                 output_json JSON,
                                                 status VARCHAR NOT NULL DEFAULT 'pending',
                                                 error_message TEXT,
                                                 retry_count INTEGER NOT NULL DEFAULT 0,
                                                 max_retries INTEGER NOT NULL DEFAULT 3,
                                                 timeout_ms INTEGER,
                                                 started_at TIMESTAMP,
                                                 completed_at TIMESTAMP,
                                                 duration_ms INTEGER,
                                                 created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                                             );
                                             CREATE INDEX IF NOT EXISTS idx_workflow_nodes_run ON workflow_nodes(run_id);
                                             CREATE INDEX IF NOT EXISTS idx_workflow_nodes_node ON workflow_nodes(node_id);
                                             CREATE INDEX IF NOT EXISTS idx_workflow_nodes_status ON workflow_nodes(status);
                                             CREATE INDEX IF NOT EXISTS idx_workflow_nodes_type ON workflow_nodes(node_type);
                                             """;

    public const string WorkflowCheckpointsV2Ddl = """
                                                   CREATE TABLE IF NOT EXISTS workflow_checkpoints (
                                                       id VARCHAR PRIMARY KEY,
                                                       run_id VARCHAR NOT NULL,
                                                       node_id VARCHAR NOT NULL,
                                                       checkpoint_type VARCHAR NOT NULL,
                                                       state_json JSON NOT NULL,
                                                       sequence_number BIGINT NOT NULL,
                                                       created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                                       UNIQUE(run_id, node_id, sequence_number)
                                                   );
                                                   CREATE INDEX IF NOT EXISTS idx_workflow_checkpoints_run ON workflow_checkpoints(run_id);
                                                   CREATE INDEX IF NOT EXISTS idx_workflow_checkpoints_node ON workflow_checkpoints(run_id, node_id);
                                                   CREATE INDEX IF NOT EXISTS idx_workflow_checkpoints_seq ON workflow_checkpoints(run_id, sequence_number DESC);
                                                   """;

    public const string WorkflowEventsV2Ddl = """
                                              CREATE TABLE IF NOT EXISTS workflow_events (
                                                  id VARCHAR PRIMARY KEY,
                                                  run_id VARCHAR NOT NULL,
                                                  node_id VARCHAR,
                                                  event_type VARCHAR NOT NULL,
                                                  event_name VARCHAR NOT NULL,
                                                  payload_json JSON,
                                                  sequence_number BIGINT NOT NULL,
                                                  source VARCHAR,
                                                  correlation_id VARCHAR,
                                                  timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                                              );
                                              CREATE INDEX IF NOT EXISTS idx_workflow_events_run ON workflow_events(run_id);
                                              CREATE INDEX IF NOT EXISTS idx_workflow_events_node ON workflow_events(run_id, node_id);
                                              CREATE INDEX IF NOT EXISTS idx_workflow_events_type ON workflow_events(event_type);
                                              CREATE INDEX IF NOT EXISTS idx_workflow_events_seq ON workflow_events(run_id, sequence_number);
                                              CREATE INDEX IF NOT EXISTS idx_workflow_events_timestamp ON workflow_events(timestamp DESC);
                                              """;
}
