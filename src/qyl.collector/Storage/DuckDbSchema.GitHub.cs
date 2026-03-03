namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string GitHubEventsDdl = """
                                          CREATE TABLE IF NOT EXISTS github_events (
                                              event_id VARCHAR PRIMARY KEY,
                                              event_type VARCHAR NOT NULL,
                                              action VARCHAR,
                                              repo_full_name VARCHAR NOT NULL,
                                              sender VARCHAR,
                                              pr_number INTEGER,
                                              pr_url VARCHAR,
                                              ref VARCHAR,
                                              payload_json VARCHAR,
                                              created_at TIMESTAMP DEFAULT now()
                                          );
                                          CREATE INDEX IF NOT EXISTS idx_github_events_type ON github_events(event_type);
                                          CREATE INDEX IF NOT EXISTS idx_github_events_repo ON github_events(repo_full_name);
                                          CREATE INDEX IF NOT EXISTS idx_github_events_created ON github_events(created_at DESC);
                                          """;
}
