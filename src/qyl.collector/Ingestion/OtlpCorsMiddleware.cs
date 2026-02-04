namespace qyl.collector.Ingestion;

/// <summary>
///     CORS middleware specifically for OTLP endpoints.
///     Only applies to /v1/* paths (OTLP HTTP endpoints).
/// </summary>
public sealed class OtlpCorsMiddleware
{
    private readonly bool _allowAll;
    private readonly string _allowedHeadersHeader;
    private readonly HashSet<string> _allowedOrigins;
    private readonly RequestDelegate _next;
    private readonly OtlpCorsOptions _options;

    public OtlpCorsMiddleware(RequestDelegate next, OtlpCorsOptions options)
    {
        _next = next;
        _options = options;
        _allowedOrigins = options.GetOrigins().ToHashSet(StringComparer.OrdinalIgnoreCase);
        _allowedHeadersHeader = string.Join(", ", options.GetHeaders());
        _allowAll = _allowedOrigins.Contains("*");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only handle OTLP paths
        if (!OtlpConstants.IsOtlpPath(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var origin = context.Request.Headers.Origin.FirstOrDefault();

        // Handle preflight
        if (context.Request.Method == "OPTIONS")
        {
            if (IsOriginAllowed(origin))
            {
                SetCorsHeaders(context.Response, origin);
                context.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
                context.Response.Headers["Access-Control-Max-Age"] =
                    _options.MaxAge.ToString(CultureInfo.InvariantCulture);
                context.Response.StatusCode = 204;
                return;
            }

            context.Response.StatusCode = 403;
            return;
        }

        // Handle actual request
        if (IsOriginAllowed(origin))
        {
            SetCorsHeaders(context.Response, origin);
        }

        await _next(context).ConfigureAwait(false);
    }

    private bool IsOriginAllowed(string? origin) =>
        !string.IsNullOrEmpty(origin) && (_allowAll || _allowedOrigins.Contains(origin));

    private void SetCorsHeaders(HttpResponse response, string? origin)
    {
        response.Headers["Access-Control-Allow-Origin"] = _allowAll ? "*" : origin;
        response.Headers["Access-Control-Allow-Headers"] = _allowedHeadersHeader;

        // RFC 6454: Access-Control-Allow-Credentials cannot be "true" when origin is "*"
        // Browsers will reject the response if both are set
        if (!_allowAll)
        {
            response.Headers["Access-Control-Allow-Credentials"] = "true";
        }
    }
}
