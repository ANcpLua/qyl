using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Qyl.Collector.Auth;
using Qyl.Instrumentation.Instrumentation.Inventory;

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

        services.Configure<KeycloakOptions>(opts =>
        {
            opts.Authority = config[KeycloakOptions.AuthorityEnvVar];
            opts.BaseUrl = config[KeycloakOptions.BaseUrlEnvVar];
            opts.Audience = config[KeycloakOptions.AudienceEnvVar];

            var tenantClaim = config[KeycloakOptions.TenantClaimEnvVar];
            if (!string.IsNullOrWhiteSpace(tenantClaim))
                opts.TenantClaim = tenantClaim;
        });

        AddKeycloakJwtBearer(services, config, environment);

        services.AddSingleton(TimeProvider.System);

        return services;
    }

    private static void AddKeycloakJwtBearer(
        IServiceCollection services,
        IConfiguration config,
        IHostEnvironment environment)
    {
        var keycloak = new KeycloakOptions
        {
            Authority = config[KeycloakOptions.AuthorityEnvVar],
            Audience = config[KeycloakOptions.AudienceEnvVar]
        };

        if (!keycloak.IsEnabled)
            return;

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloak.Authority;
                options.Audience = keycloak.Audience;
                // Keycloak in front of TLS termination is the norm in prod; allow plain HTTP discovery in dev only.
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloak.Authority,
                    ValidateAudience = true,
                    ValidAudience = keycloak.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization(options =>
            options.AddPolicy(
                QylAgentInventoryEndpoint.AdminPolicy,
                policy => policy.RequireAuthenticatedUser()));
    }

}
