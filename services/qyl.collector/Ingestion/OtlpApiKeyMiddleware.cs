namespace Qyl.Collector.Ingestion;

internal sealed class OtlpApiKeyMiddleware(RequestDelegate next, OtlpApiKeyOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (!options.IsApiKeyMode || !OtlpConstants.IsOtlpPath(path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var apiKey = context.Request.Headers[options.HeaderName].FirstOrDefault();

        if (!ValidateApiKey(apiKey))
        {
            var challenge = $"{options.HeaderName} realm=\"qyl-otlp\"";
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = challenge;
            await context.Response.WriteAsJsonAsync(
                ContractErrorFactory.Unauthorized(challenge),
                QylSerializerContext.Default.UnauthorizedError).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private bool ValidateApiKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return false;

        if (FixedTimeEquals(key, options.PrimaryApiKey))
            return true;

        return FixedTimeEquals(key, options.SecondaryApiKey);
    }

    private static bool FixedTimeEquals(string candidate, string? expected)
    {
        if (string.IsNullOrEmpty(expected) || candidate.Length != expected.Length)
            return false;

        var diff = 0;
        for (var i = 0; i < candidate.Length; i++)
            diff |= candidate[i] ^ expected[i];

        return diff is 0;
    }
}
