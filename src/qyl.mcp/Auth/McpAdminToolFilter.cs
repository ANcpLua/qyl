using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace qyl.mcp.Auth;

/// <summary>
///     Blocks admin-tier MCP tools when Keycloak is active and the caller's JWT lacks
///     the <see cref="RequiredRole"/> realm role.
///     Role state is read from <see cref="KeycloakTokenProvider.GetCachedRoles"/>, which is
///     populated on every successful client-credentials token fetch.
/// </summary>
internal sealed class McpAdminToolFilter(
    IOptions<McpAuthOptions> options,
    KeycloakTokenProvider keycloak)
{
    /// <summary>
    ///     Keycloak realm role required for administrative MCP tools.
    /// </summary>
    public const string RequiredRole = "qyl:admin";

    /// <summary>
    ///     Tools that require <see cref="RequiredRole"/> when Keycloak is enabled.
    ///     Extend this set as destructive MCP tools are added to the server.
    /// </summary>
    private static readonly FrozenSet<string> AdminToolNames = FrozenSet.ToFrozenSet(
    (string[])
    [
        // Populated when destructive tools are implemented, e.g.:
        // "qyl.storage_clear",
        // "qyl.schema_promote",
        // "qyl.replay_delete",
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Returns a denied <see cref="CallToolResult"/> when the tool requires admin access
    ///     and the current JWT lacks the role; <see langword="null"/> otherwise (allow through).
    /// </summary>
    public CallToolResult? CheckAccess(string toolName)
    {
        if (!AdminToolNames.Contains(toolName))
            return null;

        if (!options.Value.IsKeycloakEnabled)
            return null;

        if (keycloak.GetCachedRoles().Contains(RequiredRole))
            return null;

        return new CallToolResult
        {
            IsError = true,
            Content =
            [
                new TextContentBlock
                {
                    Text = $"Access denied: '{toolName}' requires the '{RequiredRole}' Keycloak realm role."
                }
            ]
        };
    }
}
