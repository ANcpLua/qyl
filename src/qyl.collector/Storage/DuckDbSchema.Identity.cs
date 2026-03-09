// =============================================================================
// Manual schema extensions for workspace identity persistence.
// The base DuckDbSchema.g.cs is auto-generated from TypeSpec; this partial
// adds tables not yet in the TypeSpec model.
// ProjectsDdl and ProjectEnvironmentsDdl moved to DuckDbSchema.g.cs.
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

    public const string HandshakeChallengesDdl = """
                                                 CREATE TABLE IF NOT EXISTS handshake_challenges (
                                                     workspace_id VARCHAR PRIMARY KEY,
                                                     code_challenge VARCHAR NOT NULL,
                                                     created_at TIMESTAMP NOT NULL
                                                 );
                                                 """;

    public const string GitHubTokensDdl = """
                                          CREATE TABLE IF NOT EXISTS github_tokens (
                                              key VARCHAR PRIMARY KEY,
                                              token VARCHAR NOT NULL,
                                              scope VARCHAR,
                                              github_login VARCHAR,
                                              auth_method VARCHAR DEFAULT 'pat',
                                              created_at TIMESTAMP DEFAULT now()
                                          );
                                          """;
}
