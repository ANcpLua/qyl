using Grpc.AspNetCore.Server;
using Qyl.Collector.Grpc;
using Qyl.Collector.ApiKeys;

namespace Qyl.Collector.Hosting;

internal static class CollectorCoreExtensions
{
    public static CollectorPortOptions AddQylCollectorCore(
        this IServiceCollection services,
        IConfiguration config)
    {
        var ports = CollectorPortOptions.FromConfiguration(config);
        services.AddSingleton(ports);
        services.AddSingleton<CollectorStreamCapacity>();

        services.AddSingleton(TimeProvider.System);

        services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default);
        });

        services.AddGrpc(static options =>
        {
            options.ResponseCompressionLevel = CompressionLevel.Optimal;
            options.ResponseCompressionAlgorithm = "gzip";
            // The gRPC half of the API-key boundary; the HTTP half is CollectorApiKeyMiddleware.
            options.Interceptors.Add<OtlpApiKeyInterceptor>();
        });
        services.AddSingleton<OtlpApiKeyInterceptor>();

        // grpc-dotnet decompresses a request into an unbounded buffer before any interceptor runs, so every
        // decompression provider is wrapped to stop at the receive limit. PostConfigure sees the final list,
        // including the defaults gRPC adds and any provider registered after this call.
        services.PostConfigure<GrpcServiceOptions>(static options =>
        {
            var limit = options.MaxReceiveMessageSize ?? BoundedDecompressionProvider.DefaultMaxReceiveMessageSize;
            for (var i = 0; i < options.CompressionProviders.Count; i++)
            {
                if (options.CompressionProviders[i] is not BoundedDecompressionProvider)
                    options.CompressionProviders[i] = new BoundedDecompressionProvider(options.CompressionProviders[i], limit);
            }
        });

        return ports;
    }
}
