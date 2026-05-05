using Qyl.Collector.Auth;

namespace Qyl.Collector.Hosting;

public static class CollectorAuthExtensions
{
    public static IServiceCollection AddQylCollectorAuth(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment environment)
    {
        var otlpCorsOptions = new OtlpCorsOptions
        {
            AllowedOrigins = config["QYL_OTLP_CORS_ALLOWED_ORIGINS"],
            AllowedHeaders = config["QYL_OTLP_CORS_ALLOWED_HEADERS"]
        };
        services.AddSingleton(otlpCorsOptions);

        var configuredAuthMode = config["QYL_OTLP_AUTH_MODE"];
        var otlpApiKeyOptions = new OtlpApiKeyOptions
        {
            AuthMode = configuredAuthMode ?? (environment.IsDevelopment() ? "Unsecured" : "ApiKey"),
            PrimaryApiKey = config["QYL_OTLP_PRIMARY_API_KEY"],
            SecondaryApiKey = config["QYL_OTLP_SECONDARY_API_KEY"]
        };

        if (otlpApiKeyOptions.IsApiKeyMode
            && string.IsNullOrWhiteSpace(otlpApiKeyOptions.PrimaryApiKey)
            && string.IsNullOrWhiteSpace(otlpApiKeyOptions.SecondaryApiKey))
        {
            throw new InvalidOperationException(
                "OTLP auth mode is 'ApiKey' but no keys are configured. " +
                "Set QYL_OTLP_PRIMARY_API_KEY or QYL_OTLP_SECONDARY_API_KEY, " +
                "or set QYL_OTLP_AUTH_MODE=Unsecured to disable authentication.");
        }

        services.AddSingleton(otlpApiKeyOptions);

        var token = config["QYL_TOKEN"] ?? TokenGenerator.Generate();
        services.AddSingleton(new TokenAuthOptions
        {
            Token = token,
            ExcludedPaths =
            [
                "/health", "/alive", "/health/ui",
                "/v1/traces", "/v1/logs",
                "/api/",
                "/assets/",
                "/favicon.ico"
            ]
        });

        var keycloakAuthority = config["QYL_KEYCLOAK_AUTHORITY"];
        var keycloakAudience = config["QYL_KEYCLOAK_AUDIENCE"];

        if (!string.IsNullOrWhiteSpace(keycloakAuthority))
        {
            services.AddHttpClient("Keycloak").AddStandardResilienceHandler();
            services.AddSingleton<IKeycloakJwksValidator>(sp =>
                new KeycloakJwksValidator(
                    keycloakAuthority,
                    keycloakAudience,
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Keycloak"),
                    sp.GetRequiredService<ILogger<KeycloakJwksValidator>>()));
        }
        else
        {
            services.AddSingleton<IKeycloakJwksValidator>(NullKeycloakJwksValidator.Instance);
        }

        return services;
    }
}
