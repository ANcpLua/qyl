namespace Qyl.Collector.Hosting;

internal static class CollectorCoreExtensions
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

        var ringBufferCapacity = config.GetValue("QYL_RINGBUFFER_CAPACITY", 10_000);
        services.AddSingleton(new SpanRingBuffer(ringBufferCapacity));

        return ports;
    }
}
