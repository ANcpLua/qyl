using System.Net;

namespace Qyl.Collector.Hosting;

internal sealed record CollectorPortOptions
{
    public required IPAddress BindAddress { get; init; }

    public required int Http { get; init; }

    public required int OtlpHttp { get; init; }

    public required int Grpc { get; init; }

    public static CollectorPortOptions FromConfiguration(IConfiguration config)
    {
        var configuredAddress = config["QYL_BIND_ADDRESS"] ?? IPAddress.Loopback.ToString();
        if (!IPAddress.TryParse(configuredAddress, out var bindAddress))
            throw new InvalidOperationException(
                $"QYL_BIND_ADDRESS must be an IP address literal, but was '{configuredAddress}'.");

        var options = new CollectorPortOptions
        {
            BindAddress = bindAddress,
            Http = int.Parse(config["QYL_PORT"] ?? config["PORT"] ?? "5100", CultureInfo.InvariantCulture),
            OtlpHttp = int.Parse(config["QYL_OTLP_PORT"] ?? "4318", CultureInfo.InvariantCulture),
            Grpc = int.Parse(config["QYL_GRPC_PORT"] ?? "4317", CultureInfo.InvariantCulture)
        };

        ValidatePort(options.Http, nameof(Http), allowDisabled: false);
        ValidatePort(options.OtlpHttp, nameof(OtlpHttp), allowDisabled: true);
        ValidatePort(options.Grpc, nameof(Grpc), allowDisabled: true);
        return options;
    }

    private static void ValidatePort(int port, string name, bool allowDisabled)
    {
        var minimum = allowDisabled ? 0 : 1;
        if (port < minimum || port > IPEndPoint.MaxPort)
            throw new InvalidOperationException($"{name} must be between {minimum} and {IPEndPoint.MaxPort}.");
    }
}
