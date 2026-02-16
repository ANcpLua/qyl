using Microsoft.AspNetCore.WebUtilities;

namespace qyl.collector.Auth;

public static class TokenGenerator
{
    public static string Generate(int byteLength = 24)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);

        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

public sealed class TokenAuthOptions
{
    /// <summary>
    ///     HTTP header name for MCP API key authentication (Aspire pattern).
    /// </summary>
    public const string McpApiKeyHeader = "x-mcp-api-key";

    /// <summary>
    ///     Gets or sets the auth token. Auto-generates a secure token if not explicitly set.
    /// </summary>
    public string Token
    {
        get => field ??= TokenGenerator.Generate();
        set;
    }

    /// <summary>
    ///     Gets or sets the cookie name for auth token storage.
    /// </summary>
    public string CookieName { get; set; } = "qyl_options.Token";

    /// <summary>
    ///     Gets or sets cookie expiration in days. Must be positive.
    /// </summary>
    public int CookieExpirationDays
    {
        get;
        set => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Cookie expiration must be positive");
    } = 3;

    /// <summary>
    ///     Gets or sets the query parameter name for token in URL.
    /// </summary>
    public string QueryParameterName { get; set; } = "t";

    /// <summary>
    ///     Gets or sets paths excluded from token authentication.
    ///     Only health probes and OTLP ingestion paths are excluded.
    ///     All /api/* endpoints REQUIRE authentication.
    /// </summary>
    public string[] ExcludedPaths { get; set; } =
        ["/health", "/ready", "/alive", "/v1/traces", "/v1/logs", "/v1/metrics"];
}

public sealed class TokenAuthMiddleware(RequestDelegate next, TokenAuthOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        // Allow dashboard root and static files without auth
        if (path == "/" || IsStaticFile(path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (options.ExcludedPaths.Any(path.StartsWithIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var queryToken = context.Request.Query[options.QueryParameterName].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryToken) && ValidateToken(queryToken))
        {
            SetAuthCookie(context);

            var cleanUrl = RemoveQueryParameter(context.Request, options.QueryParameterName);
            context.Response.Redirect(cleanUrl);
            return;
        }

        var cookieToken = context.Request.Cookies[options.CookieName];
        if (!string.IsNullOrEmpty(cookieToken) && ValidateToken(cookieToken))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader))
        {
            var bearerToken = authHeader.StartsWithIgnoreCase("Bearer ")
                ? authHeader[7..]
                : authHeader;

            if (ValidateToken(bearerToken))
            {
                await next(context).ConfigureAwait(false);
                return;
            }
        }

        // Check x-mcp-api-key header (Aspire pattern for MCP server authentication)
        var mcpApiKey = context.Request.Headers[TokenAuthOptions.McpApiKeyHeader].FirstOrDefault();
        if (!string.IsNullOrEmpty(mcpApiKey) && ValidateToken(mcpApiKey))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (path.StartsWithIgnoreCase("/api/"))
        {
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Unauthorized", "Valid token required"),
                    QylSerializerContext.Default.ErrorResponse)
                .ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private bool ValidateToken(string token) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token),
            Encoding.UTF8.GetBytes(options.Token));

    private void SetAuthCookie(HttpContext context) =>
        context.Response.Cookies.Append(options.CookieName, options.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = TimeProvider.System.GetUtcNow().AddDays(options.CookieExpirationDays),
                Path = "/"
            });

    private static string RemoveQueryParameter(HttpRequest request, string paramName)
    {
        var query = QueryHelpers.ParseQuery(request.QueryString.Value);
        query.Remove(paramName);

        var newQuery = QueryHelpers.AddQueryString(
            request.PathBase + request.Path, query);

        return newQuery;
    }

    public string GetToken() => options.Token;

    private static bool IsStaticFile(string path)
    {
        // Common static file extensions for dashboard assets
        ReadOnlySpan<string> staticExtensions =
        [
            ".js", ".css", ".html", ".htm", ".json",
            ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".webp",
            ".woff", ".woff2", ".ttf", ".eot",
            ".map", ".txt"
        ];

        foreach (var ext in staticExtensions)
        {
            if (path.EndsWithIgnoreCase(ext))
                return true;
        }

        return false;
    }
}

public sealed record LoginRequest(string Token);

public sealed record LoginResponse(bool Success, string? Error = null);

public static class TokenAuthExtensions
{
    public static IApplicationBuilder UseTokenAuth(this IApplicationBuilder app,
        Action<TokenAuthOptions>? configure = null)
    {
        var options = new TokenAuthOptions();
        configure?.Invoke(options);

        return app.UseMiddleware<TokenAuthMiddleware>(options);
    }

    public static IEndpointRouteBuilder MapLoginEndpoint(this IEndpointRouteBuilder endpoints,
        TokenAuthMiddleware middleware)
    {
        endpoints.MapPost("/api/login", (LoginRequest request, HttpContext context) =>
        {
            var options = new TokenAuthOptions();
            var isValid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(request.Token),
                Encoding.UTF8.GetBytes(middleware.GetToken()));

            if (isValid)
            {
                context.Response.Cookies.Append(options.CookieName, request.Token,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = context.Request.IsHttps,
                        SameSite = SameSiteMode.Strict,
                        Expires = TimeProvider.System.GetUtcNow().AddDays(options.CookieExpirationDays),
                        Path = "/"
                    });

                return Results.Ok(new LoginResponse(true));
            }

            return Results.BadRequest(new LoginResponse(false, "Invalid token"));
        });

        return endpoints;
    }
}
