using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace qyl.collector.Auth;

public sealed class TokenAuthOptions
{
    public string? Token { get; set; }

    public string CookieName { get; set; } = "qyl_token";

    public int CookieExpirationDays { get; set; } = 3;

    public string QueryParameterName { get; set; } = "t";

    public string[] ExcludedPaths { get; set; } = ["/health", "/ready", "/v1/traces"];
}

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

        if (_options.ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var queryToken = context.Request.Query[_options.QueryParameterName].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryToken) && ValidateToken(queryToken))
        {
            SetAuthCookie(context);

            var cleanUrl = RemoveQueryParameter(context.Request, _options.QueryParameterName);
            context.Response.Redirect(cleanUrl);
            return;
        }

        var cookieToken = context.Request.Cookies[_options.CookieName];
        if (!string.IsNullOrEmpty(cookieToken) && ValidateToken(cookieToken))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

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

        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Unauthorized", "Valid token required"),
                    QylSerializerContext.Default.ErrorResponse)
                .ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private bool ValidateToken(string token) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token),
            Encoding.UTF8.GetBytes(_token));

    private void SetAuthCookie(HttpContext context) =>
        context.Response.Cookies.Append(_options.CookieName, _token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = TimeProvider.System.GetUtcNow().AddDays(_options.CookieExpirationDays),
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

    public string GetToken() => _token;
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
