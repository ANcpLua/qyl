using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore.Authentication;
using qyl.mcp.Agents;
using qyl.mcp.Apps.ErrorExplorer;
using ModelContextProtocol.Authentication;
using qyl.mcp.Auth;
using qyl.mcp.Capabilities;
using qyl.mcp.Metadata;
using qyl.mcp.Scoping;
using qyl.mcp.Skills;
using qyl.mcp.Tools;
using qyl.mcp.Tools.Debug;
using qyl.mcp.Tools.Lsp;
using AnalyticsJsonContext = qyl.mcp.Tools.AnalyticsJsonContext;
using AnomalyJsonContext = qyl.mcp.Tools.AnomalyJsonContext;
using ErrorJsonContext = qyl.mcp.Tools.ErrorJsonContext;
using GenAiJsonContext = qyl.mcp.Tools.GenAiJsonContext;
using LogsJsonContext = qyl.mcp.Tools.LogsJsonContext;
using ReplayJsonContext = qyl.mcp.Tools.ReplayJsonContext;
using ServiceMcpJsonContext = qyl.mcp.Tools.ServiceMcpJsonContext;
using SpanQueryJsonContext = qyl.mcp.Tools.SpanQueryJsonContext;
using StorageHealthJsonContext = qyl.mcp.Tools.StorageHealthJsonContext;
using SummaryJsonContext = qyl.mcp.Tools.SummaryJsonContext;
using TelemetryJsonContext = qyl.mcp.Tools.TelemetryJsonContext;
using TelemetryToolsJsonContext = qyl.mcp.Tools.TelemetryToolsJsonContext;
using LoomMcpJsonContext = Qyl.Contracts.Loom.LoomMcpJsonContext;

namespace qyl.mcp.Hosting;

internal static class QylMcpServiceCollectionExtensions
{
    public static void ConfigureLogging(ILoggingBuilder logging) =>
        logging.AddConsole(static options => options.LogToStandardErrorThreshold = LogLevel.Trace);

    public static JsonSerializerOptions AddQylMcpCommonServices(
        this IServiceCollection services,
        IConfiguration configuration,
        SkillConfiguration skills,
        QylScope scope)
    {
        services.AddRedaction();
        services.AddMcpAuth(configuration);
        services.AddSingleton(skills);
        services.AddSingleton(scope);
        services.AddSingleton<CapabilityTools>();

        var collectorUrl = configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
        services.AddCollectorHttpClient(collectorUrl);

        Qyl.Generated.QylToolManifest.RegisterServices(services, skills);

        services.AddCollectorToolClient<ArtifactTools>();
        services.AddSingleton(TimeProvider.System);

        if (skills.IsEnabled(QylSkillKind.Debug))
        {
            services.AddSingleton<JetBrainsDiscovery>();
            services.AddSingleton<RiderMcpProxy>();

            // LSP runtime — singletons for pure lookup/state services.
            services.AddSingleton<LspServerDefinitions>();
            services.AddSingleton<LspLanguageMappings>();
            services.AddSingleton<LspServerInstallation>();
            services.AddSingleton<LspServerResolution>();
            services.AddSingleton<LspClientWrapper>();
            services.AddSingleton<WorkspaceEditApplier>();

            // LSP process lifecycle — hosted services tear down LSP servers on host shutdown.
            services.AddHostedService<LspManagerProcessCleanup>();
            services.AddHostedService<LspManagerTempDirectoryCleanup>();
        }

        services.AddSingleton<ITelemetryStore, HttpTelemetryStore>();

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.TypeInfoResolverChain.Add(TelemetryJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(TelemetryToolsJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(LogsJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(GenAiJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(StorageHealthJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(SpanQueryJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(ReplayJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(AnalyticsJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(ServiceMcpJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(ErrorJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(AnomalyJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(SummaryJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(LoomMcpJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(ErrorExplorerJsonContext.Default);

        return jsonOptions;
    }

    public static void AddQylMcpHttpAuthentication(this IServiceCollection services, McpHostOptions hostOptions)
    {
        services.AddAuthorization();

        if (!hostOptions.RequiresAuthentication)
            return;

        var authority = hostOptions.KeycloakAuthority!;

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = McpAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = authority.StartsWithIgnoreCase("https://");
                options.MapInboundClaims = false;

                if (!string.IsNullOrWhiteSpace(hostOptions.KeycloakAudience))
                {
                    options.Audience = hostOptions.KeycloakAudience;
                    options.TokenValidationParameters.ValidateAudience = true;
                }
            })
            .AddMcp(options =>
            {
                options.ForwardAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                options.Events = new McpAuthenticationEvents
                {
                    OnResourceMetadataRequest = context =>
                    {
                        var resourceUrl = hostOptions.ResolvePublicMcpUrl(context.HttpContext.Request);
                        context.ResourceMetadata = new ProtectedResourceMetadata
                        {
                            Resource = resourceUrl,
                            AuthorizationServers = [authority],
                            BearerMethodsSupported = McpAuthMetadata.BearerMethodsSupported,
                            ResourceName = QylServerMetadata.DisplayName,
                            ResourceDocumentation = QylServerMetadata.DocumentationUrl,
                        };
                        return Task.CompletedTask;
                    }
                };
            });
    }

    public static void ApplyPortFallback(IWebHostBuilder webHost, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration["ASPNETCORE_URLS"]) ||
            !string.IsNullOrWhiteSpace(configuration["DOTNET_URLS"]) ||
            !string.IsNullOrWhiteSpace(configuration["URLS"]))
        {
            return;
        }

        if (!int.TryParse(configuration["PORT"], out var port) || port <= 0)
            return;

        webHost.UseUrls($"http://0.0.0.0:{port}");
    }
}

file static class McpAuthMetadata
{
    public static readonly string[] BearerMethodsSupported = ["header"];
}
