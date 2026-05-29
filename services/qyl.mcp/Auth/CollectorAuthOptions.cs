using Duende.AccessTokenManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace qyl.mcp.Auth;

internal sealed class CollectorAuthOptions
{
    public const string AuthorityEnvVar = "QYL_KEYCLOAK_AUTHORITY";
    public const string ClientIdEnvVar = "QYL_KEYCLOAK_CLIENT_ID";
    public const string ClientSecretEnvVar = "QYL_KEYCLOAK_CLIENT_SECRET";
    public const string ScopeEnvVar = "QYL_KEYCLOAK_SCOPE";

    public string? Authority { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? Scope { get; init; }

    public bool IsClientCredentialsConfigured =>
        !string.IsNullOrWhiteSpace(Authority) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);

    public Uri TokenEndpoint =>
        new($"{Authority!.TrimEnd('/')}/protocol/openid-connect/token");

    public static CollectorAuthOptions FromConfiguration(IConfiguration configuration) =>
        new()
        {
            Authority = configuration[AuthorityEnvVar],
            ClientId = configuration[ClientIdEnvVar],
            ClientSecret = configuration[ClientSecretEnvVar],
            Scope = configuration[ScopeEnvVar] ?? "qyl.collector"
        };
}

internal static class CollectorAuthExtensions
{
    public static IServiceCollection AddCollectorClientCredentials(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = CollectorAuthOptions.FromConfiguration(configuration);
        services.AddSingleton(options);

        if (!options.IsClientCredentialsConfigured)
            return services;

        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse(McpCollectorHttpClientExtensions.CollectorClientCredentialsName),
                client =>
                {
                    client.TokenEndpoint = options.TokenEndpoint;
                    client.ClientId = ClientId.Parse(options.ClientId!);
                    client.ClientSecret = ClientSecret.Parse(options.ClientSecret!);
                    client.Scope = Scope.Parse(options.Scope!);
                });

        services.MarkCollectorClientCredentialsConfigured();
        return services;
    }

    public static bool IsCollectorClientCredentialsConfigured(this IServiceCollection services) =>
        services.Any(static descriptor => descriptor.ServiceType == typeof(CollectorClientCredentialsConfigured));

    private static void MarkCollectorClientCredentialsConfigured(this IServiceCollection services) =>
        services.AddSingleton<CollectorClientCredentialsConfigured>();

    private sealed class CollectorClientCredentialsConfigured;
}
