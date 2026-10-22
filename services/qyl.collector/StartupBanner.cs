using Qyl.Collector.ApiKeys;
using Qyl.Collector.Hosting;

namespace Qyl.Collector;

internal static class StartupBanner
{
    public static void Print(
        CollectorPortOptions ports,
        OtlpCorsOptions corsOptions,
        OtlpApiKeyOptions apiKeyOptions)
    {
        var baseUrl = $"http://localhost:{ports.Http}";
        var grpcPort = ports.Grpc;
        var httpReceiver = ports.OtlpHttp > 0
            ? $"http://localhost:{ports.OtlpHttp}"
            : baseUrl;

        Console.WriteLine();
        Console.WriteLine("qyl collector ready");
        Console.WriteLine($"  product API        {baseUrl}");
        Console.WriteLine($"  OTLP/HTTP         {httpReceiver} (/v1/traces, /v1/logs, /v1/metrics)");
        Console.WriteLine(grpcPort > 0
            ? $"  OTLP/gRPC         localhost:{grpcPort}"
            : "  OTLP/gRPC         disabled");
        Console.WriteLine($"  readiness         {baseUrl}/health");

        if (corsOptions.IsEnabled)
            Console.WriteLine($"  CORS origins      {corsOptions.AllowedOrigins}");

        Console.WriteLine(apiKeyOptions.IsApiKeyMode
            ? $"  authentication    {OtlpConstants.ApiKeyHeaderName} required"
            : "  authentication    unsecured");
        Console.WriteLine();
    }
}
