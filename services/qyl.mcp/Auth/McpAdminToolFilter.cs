using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace qyl.mcp.Auth;

internal sealed class McpAdminToolFilter(
    IOptions<McpAuthOptions> options,
    KeycloakTokenProvider keycloak)
{
    public const string RequiredRole = "qyl:admin";

    private static readonly FrozenSet<string> s_adminToolNames = ((string[])
    [
    ]).ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public CallToolResult? CheckAccess(string toolName)
    {
        if (!s_adminToolNames.Contains(toolName))
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
