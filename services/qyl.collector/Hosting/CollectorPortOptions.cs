namespace Qyl.Collector.Hosting;

internal sealed record CollectorPortOptions
{
    public required int Http { get; init; }

    public required int OtlpHttp { get; init; }

    public required int Grpc { get; init; }

    public static CollectorPortOptions FromConfiguration(IConfiguration config) =>
        new()
        {
            Http = config.GetValue<int?>("QYL_PORT")
                   ?? config.GetValue<int?>("PORT")
                   ?? 5100,
            OtlpHttp = config.GetValue("QYL_OTLP_PORT", 4318),
            Grpc = config.GetValue("QYL_GRPC_PORT", 4317)
        };
}
