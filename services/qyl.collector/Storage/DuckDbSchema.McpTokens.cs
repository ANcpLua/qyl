namespace Qyl.Collector.Storage;

public static partial class DuckDbSchema
{
    public const string McpTokensDdl = """
                                       CREATE TABLE IF NOT EXISTS mcp_tokens (
                                           token_hash VARCHAR PRIMARY KEY,
                                           user_id VARCHAR NOT NULL,
                                           tenant_id VARCHAR NOT NULL,
                                           scopes VARCHAR NOT NULL,
                                           encrypted_refresh BLOB NOT NULL,
                                           refresh_expires_at TIMESTAMP NOT NULL,
                                           created_at TIMESTAMP NOT NULL DEFAULT now(),
                                           last_used_at TIMESTAMP NOT NULL DEFAULT now(),
                                           revoked_at TIMESTAMP
                                       );
                                       CREATE INDEX IF NOT EXISTS idx_mcp_tokens_user ON mcp_tokens(user_id);
                                       CREATE INDEX IF NOT EXISTS idx_mcp_tokens_tenant ON mcp_tokens(tenant_id);
                                       CREATE INDEX IF NOT EXISTS idx_mcp_tokens_expiry ON mcp_tokens(refresh_expires_at);
                                       """;

    public const string McpPkceStateDdl = """
                                          CREATE TABLE IF NOT EXISTS mcp_pkce_state (
                                              state VARCHAR PRIMARY KEY,
                                              code_verifier VARCHAR NOT NULL,
                                              tenant_id VARCHAR NOT NULL,
                                              client_redirect_uri VARCHAR NOT NULL,
                                              nonce VARCHAR NOT NULL,
                                              created_at TIMESTAMP NOT NULL DEFAULT now(),
                                              expires_at TIMESTAMP NOT NULL
                                          );
                                          CREATE INDEX IF NOT EXISTS idx_mcp_pkce_expiry ON mcp_pkce_state(expires_at);
                                          """;
}
