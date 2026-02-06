namespace qyl.collector.Ingestion;

/// <summary>
///     API key authentication for OTLP endpoints.
/// </summary>
public sealed class OtlpApiKeyMiddleware(RequestDelegate next, OtlpApiKeyOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only validate OTLP paths when API key mode is enabled
        if (!options.IsApiKeyMode || !OtlpConstants.IsOtlpPath(path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Skip OPTIONS (preflight)
        if (context.Request.Method == "OPTIONS")
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var apiKey = context.Request.Headers[options.HeaderName].FirstOrDefault();

        if (!ValidateApiKey(apiKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Unauthorized","message":"Valid x-otlp-api-key header required"}"""
            ).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private bool ValidateApiKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return false;

        var keyBytes = Encoding.UTF8.GetBytes(key);

        // Check primary key
        if (!string.IsNullOrEmpty(options.PrimaryApiKey))
        {
            var primaryBytes = Encoding.UTF8.GetBytes(options.PrimaryApiKey);
            if (CryptographicOperations.FixedTimeEquals(keyBytes, primaryBytes))
                return true;
        }

        // Check secondary key
        if (!string.IsNullOrEmpty(options.SecondaryApiKey))
        {
            var secondaryBytes = Encoding.UTF8.GetBytes(options.SecondaryApiKey);
            if (CryptographicOperations.FixedTimeEquals(keyBytes, secondaryBytes))
                return true;
        }

        return false;
    }
}
