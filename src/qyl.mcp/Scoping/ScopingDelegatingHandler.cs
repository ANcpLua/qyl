namespace qyl.mcp.Scoping;

/// <summary>
///     HTTP delegating handler that appends scope query parameters to all collector requests.
///     When QYL_SERVICE or QYL_SESSION is set, every outgoing request gets narrowed automatically.
/// </summary>
internal sealed class ScopingDelegatingHandler(QylScope scope) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!scope.HasScope || request.RequestUri is null)
            return base.SendAsync(request, cancellationToken);

        UriBuilder uriBuilder = new(request.RequestUri);
        string existing = uriBuilder.Query.TrimStart('?');

        List<string> parts = string.IsNullOrEmpty(existing) ? [] : [existing];

        if (scope.ServiceName is not null)
            parts.Add($"service={Uri.EscapeDataString(scope.ServiceName)}");

        if (scope.SessionId is not null)
            parts.Add($"sessionId={Uri.EscapeDataString(scope.SessionId)}");

        uriBuilder.Query = string.Join("&", parts);
        request.RequestUri = uriBuilder.Uri;

        return base.SendAsync(request, cancellationToken);
    }
}
