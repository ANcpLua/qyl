using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace qyl.mcp.Auth;

public static class McpAuthExtensions
{
    public static IServiceCollection AddMcpAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<McpAuthOptions>(options =>
        {
            configuration.GetSection(McpAuthOptions.SectionName).Bind(options);

            var envToken = Environment.GetEnvironmentVariable(McpAuthOptions.TokenEnvVar);
            if (!string.IsNullOrWhiteSpace(envToken))
                options.Token = envToken;

            var authority = Environment.GetEnvironmentVariable(McpAuthOptions.KeycloakAuthorityEnvVar);
            if (!string.IsNullOrWhiteSpace(authority))
                options.KeycloakAuthority = authority;

            var clientId = Environment.GetEnvironmentVariable(McpAuthOptions.KeycloakClientIdEnvVar);
            if (!string.IsNullOrWhiteSpace(clientId))
                options.KeycloakClientId = clientId;

            var clientSecret = Environment.GetEnvironmentVariable(McpAuthOptions.KeycloakClientSecretEnvVar);
            if (!string.IsNullOrWhiteSpace(clientSecret))
                options.KeycloakClientSecret = clientSecret;
        });

        services.AddHttpClient(KeycloakTokenProvider.HttpClientName)
            .AddStandardResilienceHandler();

        services.AddSingleton<KeycloakTokenProvider>(sp =>
            new KeycloakTokenProvider(
                sp.GetRequiredService<IOptions<McpAuthOptions>>(),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(KeycloakTokenProvider.HttpClientName),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<KeycloakTokenProvider>>()));

        services.AddSingleton<McpAdminToolFilter>();

        services.AddTransient<McpAuthHandler>();

        return services;
    }

    public static IHttpClientBuilder AddMcpAuthHandler(this IHttpClientBuilder builder) =>
        builder.AddHttpMessageHandler<McpAuthHandler>();
}
