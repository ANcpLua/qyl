using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
            options.ListenAnyIP(ports.Http, lo => lo.Protocols = HttpProtocols.Http1AndHttp2);

            if (ports.OtlpHttp > 0 && ports.OtlpHttp != ports.Http)
                options.ListenAnyIP(ports.OtlpHttp, lo => lo.Protocols = HttpProtocols.Http1AndHttp2);

            if (ports.Grpc > 0)
                options.ListenAnyIP(ports.Grpc, lo => lo.Protocols = HttpProtocols.Http2);
        });

        return webHost;
    }
}
