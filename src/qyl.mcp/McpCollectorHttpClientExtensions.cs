using Microsoft.Extensions.DependencyInjection;
using qyl.mcp.Auth;

namespace qyl.mcp;

internal static class McpCollectorHttpClientExtensions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public static IHttpClientBuilder AddCollectorToolClient<TClient>(
        this IServiceCollection services,
        string collectorUrl,
        TimeSpan? timeout = null)
        where TClient : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectorUrl);

        if (!Uri.TryCreate(collectorUrl, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException($"Invalid QYL_COLLECTOR_URL '{collectorUrl}'.");
        }

        var httpClientBuilder = services
            .AddHttpClient<TClient>(client =>
            {
                client.BaseAddress = baseAddress;
                client.Timeout = timeout ?? DefaultTimeout;
            })
            .AddMcpAuthHandler()
            .AddExtendedHttpClientLogging();

        httpClientBuilder.AddStandardResilienceHandler();
        return httpClientBuilder;
    }
}
