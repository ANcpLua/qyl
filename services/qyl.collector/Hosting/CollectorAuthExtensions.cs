using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
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

        var token = config["QYL_TOKEN"] ?? TokenGenerator.Generate();
        services.AddSingleton(new TokenAuthOptions
        {
            Token = token,
            ExcludedPaths =
            [
                "/health", "/alive", "/health/ui",
                "/v1/traces", "/v1/logs", "/v1/profiles",
                "/api/",
                "/assets/",
                "/favicon.ico",
                "/auth/",
                "/mcp/"
            ]
        });

        var keycloakAuthority = config[KeycloakOptions.AuthorityEnvVar];
        var keycloakAudience = config[KeycloakOptions.AudienceEnvVar];

        services.Configure<KeycloakOptions>(opts =>
        {
            opts.Authority = keycloakAuthority;
            opts.Audience = keycloakAudience;
            opts.ClientId = config[KeycloakOptions.ClientIdEnvVar];
            opts.ClientSecret = config[KeycloakOptions.ClientSecretEnvVar];
            var allowed = config[KeycloakOptions.AllowedRedirectsEnvVar];
            if (!string.IsNullOrWhiteSpace(allowed))
            {
                opts.AllowedRedirects = allowed
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        });

        if (!string.IsNullOrWhiteSpace(keycloakAuthority))
        {
            services.AddHttpClient(KeycloakClient.HttpClientName).AddStandardResilienceHandler();
            services.AddSingleton<IKeycloakJwksValidator>(sp =>
                new KeycloakJwksValidator(
                    keycloakAuthority,
                    keycloakAudience,
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient(KeycloakClient.HttpClientName),
                    sp.GetRequiredService<ILogger<KeycloakJwksValidator>>()));
            services.AddSingleton<IKeycloakClient>(sp =>
                new KeycloakClient(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient(KeycloakClient.HttpClientName),
                    sp.GetRequiredService<IOptions<KeycloakOptions>>(),
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<KeycloakClient>>()));
        }
        else
        {
            services.AddSingleton<IKeycloakJwksValidator>(NullKeycloakJwksValidator.Instance);
            services.AddSingleton<IKeycloakClient>(NullKeycloakClient.Instance);
        }

        services.AddSingleton(TimeProvider.System);

        services.Configure<TokenEncryptionOptions>(opts =>
            opts.Key = config[TokenEncryptionOptions.KeyEnvVar]);
        services.AddSingleton<ITokenEncryption, AesGcmTokenEncryption>();

        services.AddSingleton<IMcpTokenStore>(sp => new McpTokenStore(
            sp.GetRequiredService<DuckDbStore>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IPkceStateStore>(sp => new PkceStateStore(
            sp.GetRequiredService<DuckDbStore>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddHostedService<McpTokenCleanupService>();

        return services;
    }

    public const string McpTenantAuthEnabledEnvVar = "QYL_MCP_TENANT_AUTH_ENABLED";
    public const string McpTenantPolicy = "McpTenant";

    public static bool IsMcpTenantAuthEnabled(IConfiguration config) =>
        config.GetValue<bool>(McpTenantAuthEnabledEnvVar);

    // Registers the BearerOpaque scheme (opaque-token validation against IMcpTokenStore, tenant-bound),
    // the RFC 9728 protected-resource-metadata endpoint, and the per-tenant "McpTenant" authz policy.
    // Call only when QYL_MCP_TENANT_AUTH_ENABLED is set — until the realm-scoped mint (PR-2) lands the
    // tenant boundary is forgeable, so Program.cs gates the endpoint behind the same flag.
    public static IServiceCollection AddQylMcpAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(BearerOpaqueTokenAuthenticationOptions.SchemeName)
            .AddScheme<BearerOpaqueTokenAuthenticationOptions, BearerOpaqueTokenAuthenticationHandler>(
                BearerOpaqueTokenAuthenticationOptions.SchemeName,
                static options => options.ForwardChallenge = McpAuthenticationDefaults.AuthenticationScheme)
            .AddMcp(static options => options.Events.OnResourceMetadataRequest = QylMcpResourceMetadata.PopulateAsync);

        services.AddAuthorizationBuilder()
            .AddPolicy(McpTenantPolicy, static policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(BearerOpaqueTokenAuthenticationHandler.TenantClaimType));

        return services;
    }
}
