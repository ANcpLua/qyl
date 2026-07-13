namespace Qyl.Collector;

internal static class StartupBanner
{
    public static void Print(
        string baseUrl,
        int grpcPort = 4317,
        int otlpHttpPort = 4318,
        OtlpCorsOptions? corsOptions = null,
        OtlpApiKeyOptions? apiKeyOptions = null)
    {
        var httpReceiver = otlpHttpPort > 0
            ? $"http://localhost:{otlpHttpPort}"
            : baseUrl;

        Console.WriteLine();
        Console.WriteLine("qyl collector ready");
        Console.WriteLine($"  {HttpSurfaceLabel(apiKeyOptions),-19}{baseUrl}");
        Console.WriteLine($"  OTLP/HTTP         {httpReceiver} (/v1/traces, /v1/logs, /v1development/profiles)");
        Console.WriteLine(grpcPort > 0
            ? $"  OTLP/gRPC         localhost:{grpcPort}"
            : "  OTLP/gRPC         disabled");
        Console.WriteLine($"  readiness         {baseUrl}/health");

        if (corsOptions?.IsEnabled == true)
            Console.WriteLine($"  CORS origins      {corsOptions.AllowedOrigins}");

        Console.WriteLine(apiKeyOptions?.IsApiKeyMode == true
            ? "  authentication    x-otlp-api-key required"
            : "  authentication    unsecured");
        Console.WriteLine();
    }

    internal static string HttpSurfaceLabel(OtlpApiKeyOptions? apiKeyOptions) =>
        apiKeyOptions?.IsApiKeyMode == true ? "product API" : "dashboard + API";
}
