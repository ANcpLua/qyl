using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.AspNetCore.Authentication;
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

        services.Configure<KeycloakOptions>(opts =>
        {
            opts.Authority = config[KeycloakOptions.AuthorityEnvVar];
            opts.BaseUrl = config[KeycloakOptions.BaseUrlEnvVar];
            opts.Audience = config[KeycloakOptions.AudienceEnvVar];

            var tenantClaim = config[KeycloakOptions.TenantClaimEnvVar];
            if (!string.IsNullOrWhiteSpace(tenantClaim))
                opts.TenantClaim = tenantClaim;
        });

        services.AddSingleton(TimeProvider.System);

        return services;
    }

    public const string McpTenantAuthEnabledEnvVar = "QYL_MCP_TENANT_AUTH_ENABLED";
    public const string McpTenantPolicy = "McpTenant";

    public static bool IsMcpTenantAuthEnabled(IConfiguration config) =>
        config.GetValue<bool>(McpTenantAuthEnabledEnvVar);

    // Registers JWT bearer validation against Keycloak, the RFC 9728 protected-resource-metadata endpoint,
    // and the "McpTenant" policy. The policy authenticates the token (401 if missing/invalid) then enforces
    // route {tenant} == the configured JWT tenant claim via McpTenantMatchRequirement (403 on mismatch).
    // Call only when QYL_MCP_TENANT_AUTH_ENABLED is set; Program.cs gates the endpoint behind the same flag.
    public static IServiceCollection AddQylMcpAuthentication(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment environment)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = config[KeycloakOptions.AuthorityEnvVar];
                options.Audience = config[KeycloakOptions.AudienceEnvVar];
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.MapInboundClaims = false;
                options.ForwardChallenge = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddMcp(static options => options.Events.OnResourceMetadataRequest = QylMcpResourceMetadata.PopulateAsync);

        services.AddAuthorizationBuilder()
            .AddPolicy(McpTenantPolicy, static policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new McpTenantMatchRequirement()));

        services.AddSingleton<IAuthorizationHandler, McpTenantMatchAuthorizationHandler>();

        return services;
    }

    public static void EnsureMcpTenantAuthConfiguration(IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config[KeycloakOptions.AuthorityEnvVar]))
            throw new InvalidOperationException($"{KeycloakOptions.AuthorityEnvVar} is required when {McpTenantAuthEnabledEnvVar}=true.");
        if (string.IsNullOrWhiteSpace(config[KeycloakOptions.AudienceEnvVar]))
            throw new InvalidOperationException($"{KeycloakOptions.AudienceEnvVar} is required when {McpTenantAuthEnabledEnvVar}=true.");
    }
}
