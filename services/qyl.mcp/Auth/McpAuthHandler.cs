using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace qyl.mcp.Auth;

public sealed partial class McpAuthHandler : DelegatingHandler
{
    private readonly KeycloakTokenProvider _keycloak;
    private readonly ILogger<McpAuthHandler> _logger;
    private readonly McpAuthOptions _options;

    public McpAuthHandler(
        IOptions<McpAuthOptions> options,
        KeycloakTokenProvider keycloak,
        ILogger<McpAuthHandler> logger)
    {
        _options = options.Value;
        _keycloak = keycloak;
        _logger = logger;

        if (_options.IsKeycloakEnabled)
            LogKeycloakEnabled();
        else if (_options.IsEnabled)
            LogAuthEnabled();
        else
            LogAuthDisabled();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_options.IsKeycloakEnabled)
        {
            var jwt = await _keycloak.GetTokenAsync(cancellationToken).ConfigureAwait(false);
            if (jwt is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            LogKeycloakFallback();
        }

        if (_options.IsEnabled && !string.IsNullOrWhiteSpace(_options.Token))
        {
            request.Headers.TryAddWithoutValidation(McpAuthOptions.HeaderName, _options.Token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "MCP authentication using Keycloak OAuth2 client-credentials")]
    private partial void LogKeycloakEnabled();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak token unavailable — falling back to x-mcp-api-key")]
    private partial void LogKeycloakFallback();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "MCP authentication enabled - adding x-mcp-api-key header to collector requests")]
    private partial void LogAuthEnabled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "MCP authentication disabled - running in dev mode without auth")]
    private partial void LogAuthDisabled();
}
