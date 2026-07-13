using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Qyl.Collector.Hosting;

internal static class CollectorKestrelExtensions
{
    public static ConfigureWebHostBuilder ConfigureQylCollectorKestrel(
        this ConfigureWebHostBuilder webHost,
        CollectorPortOptions ports)
    {
        webHost.ConfigureKestrel(options =>
        {
            options.Listen(ports.BindAddress, ports.Http, lo => lo.Protocols = HttpProtocols.Http1AndHttp2);

            if (ports.OtlpHttp > 0 && ports.OtlpHttp != ports.Http)
                options.Listen(ports.BindAddress, ports.OtlpHttp, lo => lo.Protocols = HttpProtocols.Http1AndHttp2);

            if (ports.Grpc > 0)
                options.Listen(ports.BindAddress, ports.Grpc, lo => lo.Protocols = HttpProtocols.Http2);
        });

        return webHost;
    }
}
