using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace qyl.mcp.Auth;

/// <summary>
///     Extension methods for configuring MCP authentication.
/// </summary>
public static class McpAuthExtensions
{
    /// <summary>
    ///     Adds MCP authentication services to the service collection.
    ///     Reads token from configuration or environment variables.
    ///     When Keycloak is configured, fetches a JWT via client-credentials and forwards it as Bearer.
    ///     Falls back to x-mcp-api-key when Keycloak is not configured.
    /// </summary>
    public static IServiceCollection AddMcpAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<McpAuthOptions>(options =>
        {
            // First try configuration section
            configuration.GetSection(McpAuthOptions.SectionName).Bind(options);

            // API-key: environment variable takes precedence over config
            string? envToken = Environment.GetEnvironmentVariable(McpAuthOptions.TokenEnvVar);
            if (!string.IsNullOrWhiteSpace(envToken))
                options.Token = envToken;

            // Keycloak OAuth2 client-credentials
            string? authority = Environment.GetEnvironmentVariable(McpAuthOptions.KeycloakAuthorityEnvVar);
            if (!string.IsNullOrWhiteSpace(authority))
                options.KeycloakAuthority = authority;

            string? clientId = Environment.GetEnvironmentVariable(McpAuthOptions.KeycloakClientIdEnvVar);
            if (!string.IsNullOrWhiteSpace(clientId))
                options.KeycloakClientId = clientId;

            string? clientSecret = Environment.GetEnvironmentVariable(McpAuthOptions.KeycloakClientSecretEnvVar);
            if (!string.IsNullOrWhiteSpace(clientSecret))
                options.KeycloakClientSecret = clientSecret;
        });

        // Named HttpClient for Keycloak token endpoint with resilience (retry + circuit-breaker)
        services.AddHttpClient(KeycloakTokenProvider.HttpClientName)
            .AddStandardResilienceHandler();

        // KeycloakTokenProvider: singleton — caches JWT and realm roles across tool calls
        services.AddSingleton<KeycloakTokenProvider>(sp =>
            new KeycloakTokenProvider(
                sp.GetRequiredService<IOptions<McpAuthOptions>>(),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(KeycloakTokenProvider.HttpClientName),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<KeycloakTokenProvider>>()));

        // Admin tool filter: gates destructive tools behind qyl:admin realm role
        services.AddSingleton<McpAdminToolFilter>();

        // Register the handler for DI
        services.AddTransient<McpAuthHandler>();

        return services;
    }

    /// <summary>
    ///     Adds the MCP authentication handler to the HTTP client builder.
    ///     This handler adds Bearer JWT (Keycloak) or x-mcp-api-key header to all requests.
    /// </summary>
    public static IHttpClientBuilder AddMcpAuthHandler(this IHttpClientBuilder builder) =>
        builder.AddHttpMessageHandler<McpAuthHandler>();
}
