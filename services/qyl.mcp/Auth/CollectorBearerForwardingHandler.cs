using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace qyl.mcp.Auth;

internal sealed class CollectorBearerForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorization)
            && AuthenticationHeaderValue.TryParse(authorization, out var header)
            && string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = header;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
