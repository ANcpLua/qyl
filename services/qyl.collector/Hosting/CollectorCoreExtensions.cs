using Qyl.Collector.Cost;
using Qyl.Collector.Grpc;

namespace Qyl.Collector.Hosting;

internal static class CollectorCoreExtensions
{
    public static CollectorPortOptions AddQylCollectorCore(
        this IServiceCollection services,
        IConfiguration config)
    {
        var ports = CollectorPortOptions.FromConfiguration(config);
        services.AddSingleton(ports);
        services.AddSingleton<ModelPricingService>();

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

        return ports;
    }
}
