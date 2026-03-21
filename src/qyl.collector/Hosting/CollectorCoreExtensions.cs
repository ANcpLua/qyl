using Qyl.Collector.Grpc;
using Qyl.Collector.Observe;

namespace Qyl.Collector.Hosting;

public static class CollectorCoreExtensions
{
    public static CollectorPortOptions AddQylCollectorCore(
        this IServiceCollection services,
        IConfiguration config)
    {
        var ports = CollectorPortOptions.FromConfiguration(config);
        services.AddSingleton(ports);

        services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default);
        });

        services.AddRequestDecompression();

        services.AddGrpc(static options =>
        {
            options.ResponseCompressionLevel = CompressionLevel.Optimal;
            options.ResponseCompressionAlgorithm = "gzip";
        });
        services.AddSingleton<IServiceMethodProvider<TraceServiceImpl>, TraceServiceMethodProvider>();

        services.AddSingleton<FrontendConsole>();
        services.AddSingleton<ITelemetrySseBroadcaster, TelemetrySseBroadcaster>();

        var ringBufferCapacity = config.GetValue("QYL_RINGBUFFER_CAPACITY", 10_000);
        services.AddSingleton(new SpanRingBuffer(ringBufferCapacity));

        services.AddSingleton<SubscriptionManager>();

        return ports;
    }
}
