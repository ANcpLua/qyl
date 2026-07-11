namespace Qyl.Collector.Ingestion;

/// <summary>
/// API-key boundary for the collector's HTTP surface (repair-plan phase 5). In ApiKey mode it
/// guards BOTH the OTLP ingest routes (<c>/v1/*</c>) and the read API (<c>/api/v1/*</c>) with
/// the same key pair — one collector credential, sent as the <c>x-otlp-api-key</c> header
/// (historical name, kept as the single header on purpose). <c>/health</c>, <c>/alive</c> and
/// the SPA stay open: liveness must be probeable without credentials, and the dashboard shell
/// carries no data.
/// </summary>
/// <remarks>
/// Read-API requests may alternatively pass <c>?api_key=</c> — EventSource cannot set headers,
/// and the SSE log stream needs a credential path (ASP.NET's OTel instrumentation redacts query
/// values by default, so the key does not leak into the collector's own telemetry). The gRPC
/// ingest boundary is the mirror-image <see cref="Qyl.Collector.Grpc.OtlpApiKeyInterceptor"/>.
/// </remarks>
internal sealed class CollectorApiKeyMiddleware(RequestDelegate next, OtlpApiKeyOptions options)
{
    private const string QueryFallbackName = "api_key";

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var isReadApi = path.StartsWith("/api/v1", StringComparison.OrdinalIgnoreCase);

        if (!options.IsApiKeyMode || (!OtlpConstants.IsOtlpPath(path) && !isReadApi))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var apiKey = context.Request.Headers[options.HeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey) && isReadApi)
            apiKey = context.Request.Query[QueryFallbackName].FirstOrDefault();

        if (!OtlpApiKeyValidator.IsValid(apiKey, options))
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
}
