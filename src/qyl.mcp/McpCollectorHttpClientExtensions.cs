namespace qyl.mcp;

using Agents;
using Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Scoping;

internal static class McpCollectorHttpClientExtensions
{
    public const string CollectorClientName = "qyl.collector";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public static IHttpClientBuilder AddCollectorHttpClient(
        this IServiceCollection services,
        string collectorUrl,
        TimeSpan? timeout = null)
    {
        if (timeout is { } configuredTimeout && configuredTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
        }

        var effectiveTimeout = timeout ?? DefaultTimeout;

        ArgumentException.ThrowIfNullOrWhiteSpace(collectorUrl);

        if (!Uri.TryCreate(collectorUrl, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException($"Invalid QYL_COLLECTOR_URL '{collectorUrl}'.");
        }

        services.AddTransient<CollectorConcurrencyLimiter>();

        var httpClientBuilder = services
            .AddHttpClient(CollectorClientName, client =>
            {
                client.BaseAddress = baseAddress;
            })
            .AddHttpMessageHandler<CollectorConcurrencyLimiter>()
            .AddMcpAuthHandler()
            .AddHttpMessageHandler(static sp => new ScopingDelegatingHandler(sp.GetRequiredService<QylScope>()))
            .AddExtendedHttpClientLogging();

        // Most MCP tools ask for a bare HttpClient. Make that default resolve to the
        // collector-configured named client so tool constructors stay simple.
        services.Replace(ServiceDescriptor.Transient<HttpClient>(static sp =>
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(CollectorClientName)));

        httpClientBuilder
            .AddStandardResilienceHandler()
            .Configure(options =>
            {
                options.TotalRequestTimeout.Timeout = effectiveTimeout;

                if (options.AttemptTimeout.Timeout >= effectiveTimeout)
                {
                    options.AttemptTimeout.Timeout = effectiveTimeout > TimeSpan.FromMilliseconds(1)
                        ? effectiveTimeout - TimeSpan.FromMilliseconds(1)
                        : TimeSpan.FromTicks(Math.Max(1, effectiveTimeout.Ticks / 2));
                }
            });

        return httpClientBuilder;
    }

    public static IServiceCollection AddCollectorToolClient<TClient>(
        this IServiceCollection services)
        where TClient : class
    {
        services.AddTransient<TClient>();

        return services;
    }
}
