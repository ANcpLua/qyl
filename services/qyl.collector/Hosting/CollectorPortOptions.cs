namespace Qyl.Collector.Hosting;

public sealed record CollectorPortOptions
{
    /// <summary>Dashboard + REST API + SSE port.</summary>
    public required int Http { get; init; }

    /// <summary>OTLP HTTP ingestion port (0 = disabled, falls back to Http).</summary>
    public required int OtlpHttp { get; init; }

    /// <summary>gRPC OTLP ingestion port (0 = disabled).</summary>
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
