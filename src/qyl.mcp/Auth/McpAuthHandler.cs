using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace qyl.mcp.Auth;

/// <summary>
///     Delegating handler that adds the x-mcp-api-key header to outgoing requests.
///     Only adds the header when authentication is enabled (token is configured).
/// </summary>
public sealed partial class McpAuthHandler : DelegatingHandler
{
    private readonly ILogger<McpAuthHandler> _logger;
    private readonly McpAuthOptions _options;

    public McpAuthHandler(IOptions<McpAuthOptions> options, ILogger<McpAuthHandler> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.IsEnabled)
            LogAuthEnabled();
        else
            LogAuthDisabled();
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_options.IsEnabled && !string.IsNullOrWhiteSpace(_options.Token))
        {
            request.Headers.TryAddWithoutValidation(McpAuthOptions.HeaderName, _options.Token);
        }

        return base.SendAsync(request, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP authentication enabled - adding x-mcp-api-key header to collector requests")]
    private partial void LogAuthEnabled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "MCP authentication disabled - running in dev mode without auth")]
    private partial void LogAuthDisabled();
}
