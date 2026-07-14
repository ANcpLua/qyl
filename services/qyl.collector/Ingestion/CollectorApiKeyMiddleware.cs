namespace Qyl.Collector.Ingestion;

/// <summary>
/// API-key boundary for the collector's HTTP surface. In ApiKey mode it
/// guards both the OTLP ingest routes (<c>/v1/*</c>) and the read API (<c>/api/v1/*</c>) with
/// one collector credential sent as the generated contract's <c>x-otlp-api-key</c> header.
/// <c>/health</c>, <c>/alive</c>, and
/// the SPA stay open: liveness must be probeable without credentials, and the dashboard shell
/// carries no data.
/// </summary>
/// <remarks>
/// The dashboard uses fetch-based SSE so credentials stay in the request header and never enter
/// URLs, browser history, proxy logs, or referrers. The gRPC ingest boundary is the mirror-image
/// <see cref="Qyl.Collector.Grpc.OtlpApiKeyInterceptor"/>.
/// </remarks>
internal sealed class CollectorApiKeyMiddleware(RequestDelegate next, OtlpApiKeyOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var isReadApi = path.StartsWith("/api/v1", StringComparison.OrdinalIgnoreCase);

        if (isReadApi)
            context.Response.Headers.CacheControl = "private, no-store";

        if (!options.IsApiKeyMode || (!OtlpConstants.IsOtlpPath(path) && !isReadApi))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var apiKey = context.Request.Headers[options.HeaderName].FirstOrDefault();

        if (!OtlpApiKeyValidator.IsValid(apiKey, options))
        {
            var challenge = $"{options.HeaderName} realm=\"qyl-otlp\"";
            context.Response.Headers.WWWAuthenticate = challenge;
            if (isReadApi)
            {
                await ContractErrorResults.WriteUnauthorizedAsync(
                    context.Response,
                    challenge,
                    context.RequestAborted).ConfigureAwait(false);
            }
            else
            {
                var encoding = DetectEncoding(context.Request.ContentType);
                await OtlpHttpResult.Failure(
                        StatusCodes.Status401Unauthorized,
                        encoding,
                        "Missing or invalid API key.")
                    .ExecuteAsync(context)
                    .ConfigureAwait(false);
            }

            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static OtlpPayloadEncoding DetectEncoding(string? contentType)
    {
        try
        {
            return OtlpPayloadParser.GetEncoding(contentType);
        }
        catch (OtlpUnsupportedMediaTypeException)
        {
            // An unsupported request has no OTLP response encoding to mirror. Valid protobuf and
            // JSON requests always retain their wire type; JSON is the interoperable fallback.
            return OtlpPayloadEncoding.Json;
        }
    }
}
