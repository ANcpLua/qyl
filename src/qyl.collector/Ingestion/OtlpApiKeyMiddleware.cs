namespace qyl.collector.Ingestion;

/// <summary>
///     API key authentication for OTLP endpoints.
/// </summary>
public sealed class OtlpApiKeyMiddleware
{
    private static readonly string[] OtlpPaths = ["/v1/traces", "/v1/logs", "/v1/metrics"];
    private readonly RequestDelegate _next;
    private readonly OtlpApiKeyOptions _options;

    public OtlpApiKeyMiddleware(RequestDelegate next, OtlpApiKeyOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only validate OTLP paths when API key mode is enabled
        if (!_options.IsApiKeyMode || !IsOtlpPath(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Skip OPTIONS (preflight)
        if (context.Request.Method == "OPTIONS")
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var apiKey = context.Request.Headers[_options.HeaderName].FirstOrDefault();

        if (!ValidateApiKey(apiKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Unauthorized","message":"Valid x-otlp-api-key header required"}"""
            ).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool IsOtlpPath(string path) =>
        OtlpPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private bool ValidateApiKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return false;

        var keyBytes = Encoding.UTF8.GetBytes(key);

        // Check primary key
        if (!string.IsNullOrEmpty(_options.PrimaryApiKey))
        {
            var primaryBytes = Encoding.UTF8.GetBytes(_options.PrimaryApiKey);
            if (CryptographicOperations.FixedTimeEquals(keyBytes, primaryBytes))
                return true;
        }

        // Check secondary key
        if (!string.IsNullOrEmpty(_options.SecondaryApiKey))
        {
            var secondaryBytes = Encoding.UTF8.GetBytes(_options.SecondaryApiKey);
            if (CryptographicOperations.FixedTimeEquals(keyBytes, secondaryBytes))
                return true;
        }

        return false;
    }
}
