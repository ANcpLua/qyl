using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace qyl.mcp.Sentry;

/// <summary>
///     Delegating handler that injects <c>Authorization: Bearer {token}</c>
///     on every outbound request to the Sentry REST API.
/// </summary>
public sealed partial class qylAuthHandler : DelegatingHandler
{
    private readonly ILogger<qylAuthHandler> _logger;
    private readonly qylAuthOptions _options;

    public qylAuthHandler(IOptions<qylAuthOptions> options, ILogger<qylAuthHandler> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.IsEnabled)
            LogAuthEnabled(_options.Host);
        else
            LogAuthDisabled();
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_options.IsEnabled)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AuthToken);

        return base.SendAsync(request, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Sentry auth enabled — targeting {Host}")]
    private partial void LogAuthEnabled(string host);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SENTRY_AUTH_TOKEN not set — Sentry tools will return auth errors")]
    private partial void LogAuthDisabled();
}
