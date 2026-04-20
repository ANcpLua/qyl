namespace Qyl.Collector.Hosting;

using Microsoft.AspNetCore.Server.Kestrel.Core;

public static class CollectorKestrelExtensions
{
    public static ConfigureWebHostBuilder ConfigureQylCollectorKestrel(
        this ConfigureWebHostBuilder webHost,
        IConfiguration config)
    {
        var ports = CollectorPortOptions.FromConfiguration(config);

        webHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(ports.Http, lo => lo.Protocols = HttpProtocols.Http1AndHttp2);

            if (ports.OtlpHttp > 0 && ports.OtlpHttp != ports.Http)
                options.ListenAnyIP(ports.OtlpHttp, lo => lo.Protocols = HttpProtocols.Http1AndHttp2);

            if (ports.Grpc > 0)
                options.ListenAnyIP(ports.Grpc, lo => lo.Protocols = HttpProtocols.Http2);
        });

        return webHost;
    }
}
