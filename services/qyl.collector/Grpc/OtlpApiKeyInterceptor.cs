using Grpc.Core.Interceptors;

namespace Qyl.Collector.Grpc;

/// <summary>
/// The gRPC mirror of <see cref="Qyl.Collector.Ingestion.CollectorApiKeyMiddleware"/>: in ApiKey
/// mode every OTLP export call must carry a valid <c>x-otlp-api-key</c> metadata entry — the
/// same key pair, the same fixed-time validation, <c>Unauthenticated</c> instead of 401.
/// All OTLP export methods are unary, so the unary handler is the whole boundary.
/// </summary>
internal sealed class OtlpApiKeyInterceptor(OtlpApiKeyOptions options) : Interceptor
{
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        if (options.IsApiKeyMode &&
            !OtlpApiKeyValidator.IsValid(context.RequestHeaders.GetValue(options.HeaderName), options))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing or invalid API key."));
        }

        return continuation(request, context);
    }
}
