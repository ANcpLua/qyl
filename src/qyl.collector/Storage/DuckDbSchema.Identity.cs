// =============================================================================
// Manual schema extensions for workspace identity persistence.
// The base DuckDbSchema.g.cs is auto-generated from TypeSpec; this partial
// adds tables not yet in the TypeSpec model.
// =============================================================================

namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string WorkspacesDdl = """
        CREATE TABLE IF NOT EXISTS workspaces (
            workspace_id VARCHAR PRIMARY KEY,
            name VARCHAR,
            service_name VARCHAR,
            sdk_version VARCHAR,
            runtime_version VARCHAR,
            framework VARCHAR,
            git_commit VARCHAR,
            status VARCHAR DEFAULT 'pending',
            first_seen TIMESTAMP DEFAULT now(),
            last_heartbeat TIMESTAMP DEFAULT now(),
            metadata_json VARCHAR
        );
        CREATE INDEX IF NOT EXISTS idx_workspaces_service_name ON workspaces(service_name);
        """;

    public const string ProjectsDdl = """
        CREATE TABLE IF NOT EXISTS projects (
            project_id VARCHAR PRIMARY KEY,
            workspace_id VARCHAR NOT NULL,
            name VARCHAR NOT NULL,
            description VARCHAR,
            created_at TIMESTAMP DEFAULT now(),
            updated_at TIMESTAMP DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS idx_projects_workspace ON projects(workspace_id);
        """;

    public const string ProjectEnvironmentsDdl = """
        CREATE TABLE IF NOT EXISTS project_environments (
            environment_id VARCHAR PRIMARY KEY,
            project_id VARCHAR NOT NULL,
            name VARCHAR NOT NULL,
            description VARCHAR,
            created_at TIMESTAMP DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS idx_project_environments_project ON project_environments(project_id);
        """;

    public const string HandshakeChallengesDdl = """
        CREATE TABLE IF NOT EXISTS handshake_challenges (
            workspace_id VARCHAR PRIMARY KEY,
            code_challenge VARCHAR NOT NULL,
            created_at TIMESTAMP NOT NULL
        );
        """;
}
