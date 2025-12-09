// qyl.collector - Token Authentication
// Secure token-based authentication with console URL and persistent cookie

using System.Security.Cryptography;

namespace qyl.collector.Auth;

/// <summary>
/// Token-based authentication for qyl.collector dashboard.
/// Uses fixed-time comparison to prevent timing attacks.
/// </summary>
public sealed class TokenAuthOptions
{
    /// <summary>
    /// The authentication token. Auto-generated if not specified.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Cookie name for persisting authentication.
    /// </summary>
    public string CookieName { get; set; } = "qyl_token";

    /// <summary>
    /// Cookie expiration in days.
    /// </summary>
    public int CookieExpirationDays { get; set; } = 3;

    /// <summary>
    /// Query parameter name for token.
    /// </summary>
    public string QueryParameterName { get; set; } = "t";

    /// <summary>
    /// Paths that don't require authentication (health checks, etc.)
    /// </summary>
    public string[] ExcludedPaths { get; set; } = ["/health", "/ready", "/v1/traces"];
}

/// <summary>
/// Token authentication middleware.
/// </summary>
public sealed class TokenAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TokenAuthOptions _options;
    private readonly string _token;

    public TokenAuthMiddleware(RequestDelegate next, TokenAuthOptions options)
    {
        _next = next;
        _options = options;
        _token = options.Token ?? TokenGenerator.Generate();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        // Skip auth for excluded paths
        if (_options.ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Check query parameter first (for clickable links)
        var queryToken = context.Request.Query[_options.QueryParameterName].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryToken) && ValidateToken(queryToken))
        {
            // Set cookie and redirect to clean URL
            SetAuthCookie(context);

            // Redirect to remove token from URL (security)
            var cleanUrl = RemoveQueryParameter(context.Request, _options.QueryParameterName);
            context.Response.Redirect(cleanUrl);
            return;
        }

        // Check cookie
        var cookieToken = context.Request.Cookies[_options.CookieName];
        if (!string.IsNullOrEmpty(cookieToken) && ValidateToken(cookieToken))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Check Authorization header
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader))
        {
            var bearerToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader[7..]
                : authHeader;

            if (ValidateToken(bearerToken))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }
        }

        // API endpoints return 401, dashboard returns login page
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized", message = "Valid token required" }).ConfigureAwait(false);
            return;
        }

        // For dashboard routes, serve login page (handled by SPA)
        // The SPA will detect 401 and show login
        await _next(context).ConfigureAwait(false);
    }

    private bool ValidateToken(string token)
    {
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(token),
            System.Text.Encoding.UTF8.GetBytes(_token));
    }

    private void SetAuthCookie(HttpContext context)
    {
        context.Response.Cookies.Append(_options.CookieName, _token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(_options.CookieExpirationDays),
            Path = "/"
        });
    }

    private static string RemoveQueryParameter(HttpRequest request, string paramName)
    {
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.QueryString.Value);
        query.Remove(paramName);

        var newQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
            request.PathBase + request.Path, query);

        return newQuery;
    }

    /// <summary>
    /// Gets the current token (for console output).
    /// </summary>
    public string GetToken() => _token;
}

/// <summary>
/// Login request model.
/// </summary>
public sealed record LoginRequest(string Token);

/// <summary>
/// Login response model.
/// </summary>
public sealed record LoginResponse(bool Success, string? Error = null);

/// <summary>
/// Extension methods for token auth.
/// </summary>
public static class TokenAuthExtensions
{
    /// <summary>
    /// Adds token authentication to the application.
    /// </summary>
    public static IApplicationBuilder UseTokenAuth(this IApplicationBuilder app, Action<TokenAuthOptions>? configure = null)
    {
        var options = new TokenAuthOptions();
        configure?.Invoke(options);

        return app.UseMiddleware<TokenAuthMiddleware>(options);
    }

    /// <summary>
    /// Maps the login endpoint for token validation.
    /// </summary>
    public static IEndpointRouteBuilder MapLoginEndpoint(this IEndpointRouteBuilder endpoints, TokenAuthMiddleware middleware)
    {
        endpoints.MapPost("/api/login", (LoginRequest request, HttpContext context) =>
        {
            var options = new TokenAuthOptions();
            var isValid = CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(request.Token),
                System.Text.Encoding.UTF8.GetBytes(middleware.GetToken()));

            if (isValid)
            {
                context.Response.Cookies.Append(options.CookieName, request.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(options.CookieExpirationDays),
                    Path = "/"
                });

                return Results.Ok(new LoginResponse(true));
            }

            return Results.BadRequest(new LoginResponse(false, "Invalid token"));
        });

        return endpoints;
    }
}
