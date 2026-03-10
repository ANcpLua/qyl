using Microsoft.Extensions.DependencyInjection;
using Qyl.Mcp.Auth;
using Qyl.Mcp.Scoping;

namespace Qyl.Mcp;

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
            .AddHttpMessageHandler(static sp => new ScopingDelegatingHandler(sp.GetRequiredService<QylScope>()))
            .AddExtendedHttpClientLogging();

        httpClientBuilder.AddStandardResilienceHandler();
        return httpClientBuilder;
    }
}
