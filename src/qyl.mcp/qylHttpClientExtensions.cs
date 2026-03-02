using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace qyl.mcp.Sentry;

internal static class qylHttpClientExtensions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Reads Sentry config from environment / IConfiguration, registers options,
    ///     and wires up an <see cref="HttpClient" /> for <typeparamref name="TClient" />
    ///     with Bearer auth, resilience, and extended logging — mirroring
    ///     <c>AddCollectorToolClient</c> for qyl.collector clients.
    /// </summary>
    public static IHttpClientBuilder AddSentryToolClient<TClient>(
        this IServiceCollection services,
        IConfiguration configuration,
        TimeSpan? timeout = null)
        where TClient : class
    {
        // Register options once (idempotent via Configure)
        services.Configure<qylAuthOptions>(options =>
        {
            configuration.GetSection(qylAuthOptions.SectionName).Bind(options);

            var token = Environment.GetEnvironmentVariable(qylAuthOptions.TokenEnvVar);
            if (!string.IsNullOrWhiteSpace(token))
                options.AuthToken = token;

            var host = Environment.GetEnvironmentVariable(qylAuthOptions.HostEnvVar);
            if (!string.IsNullOrWhiteSpace(host))
                options.Host = host;

            var org = Environment.GetEnvironmentVariable(qylAuthOptions.OrgEnvVar);
            if (!string.IsNullOrWhiteSpace(org))
                options.OrgSlug = org;
        });

        services.AddTransient<qylAuthHandler>();

        // Build base URI from current env so it is resolved at startup, not per-request
        var host = Environment.GetEnvironmentVariable(qylAuthOptions.HostEnvVar) ?? "sentry.io";
        var baseUri = new Uri($"https://{host}/api/0/");

        var builder = services
            .AddHttpClient<TClient>(client =>
            {
                client.BaseAddress = baseUri;
                client.Timeout = timeout ?? DefaultTimeout;
                client.DefaultRequestHeaders.Add("User-Agent", "qyl-mcp/1.0");
            })
            .AddHttpMessageHandler<qylAuthHandler>()
            .AddExtendedHttpClientLogging();

        builder.AddStandardResilienceHandler();
        return builder;
    }
}
