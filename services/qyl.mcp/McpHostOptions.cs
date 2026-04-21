using qyl.mcp.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace qyl.mcp;

internal enum McpTransportMode
{
    Stdio,
    Http
}

internal sealed class McpHostOptions
{
    public const string TransportEnvVar = "QYL_MCP_TRANSPORT";
    public const string PathEnvVar = "QYL_MCP_PATH";
    public const string PublicUrlEnvVar = "QYL_MCP_PUBLIC_URL";
    public const string StatelessEnvVar = "QYL_MCP_STATELESS";
    public const string KeycloakAudienceEnvVar = "QYL_KEYCLOAK_AUDIENCE";

    public required McpTransportMode Transport { get; init; }

    public required string Path { get; init; }

    public string? PublicBaseUrl { get; init; }

    public bool Stateless { get; init; }

    public string? KeycloakAuthority { get; init; }

    public string? KeycloakAudience { get; init; }

    public bool UseHttpTransport => Transport is McpTransportMode.Http;

    public bool RequiresAuthentication =>
        UseHttpTransport &&
        !string.IsNullOrWhiteSpace(KeycloakAuthority);

    public static McpTransportMode ResolveTransport(string[] args)
    {
        var requested = TryGetCommandLineValue(args, TransportEnvVar)
                        ?? Environment.GetEnvironmentVariable(TransportEnvVar);

        if (TryParseTransport(requested, out var transport))
            return transport;

        return HasHttpSignals()
            ? McpTransportMode.Http
            : McpTransportMode.Stdio;
    }

    public static McpHostOptions FromConfiguration(IConfiguration configuration, McpTransportMode transport)
    {
        var path = configuration[PathEnvVar];
        if (string.IsNullOrWhiteSpace(path))
            path = "/mcp";

        if (!path.StartsWithOrdinal("/"))
            path = "/" + path;

        return new McpHostOptions
        {
            Transport = transport,
            Path = path,
            PublicBaseUrl = configuration[PublicUrlEnvVar],
            Stateless = bool.TryParse(configuration[StatelessEnvVar], out var stateless) && stateless,
            KeycloakAuthority = configuration[McpAuthOptions.KeycloakAuthorityEnvVar],
            KeycloakAudience = configuration[KeycloakAudienceEnvVar]
        };
    }

    public string ResolvePublicMcpUrl(HttpRequest request)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(PublicBaseUrl)
            ? PublicBaseUrl.TrimEnd('/')
            : $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');

        return $"{baseUrl}{Path}";
    }

    private static bool HasHttpSignals() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_URLS")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("URLS")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORT"));

    private static bool TryParseTransport(string? value, out McpTransportMode transport)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "http":
            case "streamable-http":
            case "remote":
                transport = McpTransportMode.Http;
                return true;
            case "stdio":
            case "local":
                transport = McpTransportMode.Stdio;
                return true;
            default:
                transport = default;
                return false;
        }
    }

    private static string? TryGetCommandLineValue(string[] args, string key)
    {
        var inlinePrefix = $"--{key}=";
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWithIgnoreCase(inlinePrefix))
                return arg[inlinePrefix.Length..];

            if (!arg.EqualsIgnoreCase($"--{key}"))
                continue;

            if (i + 1 < args.Length)
                return args[i + 1];
        }

        return null;
    }
}
