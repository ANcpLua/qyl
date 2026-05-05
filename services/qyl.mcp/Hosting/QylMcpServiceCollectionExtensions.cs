using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using Qyl.Generated;
using qyl.mcp.Agents;
using qyl.mcp.Apps.ErrorExplorer;
using qyl.mcp.Auth;
using qyl.mcp.Clients;
using qyl.mcp.Metadata;
using qyl.mcp.Scoping;
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

internal static partial class QylMcpServiceCollectionExtensions
{
    public static void ConfigureLogging(ILoggingBuilder logging, bool stdioTransport) =>
        logging.AddConsole(options =>
        {
            if (stdioTransport)
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            }
        });

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

        services.AddSingleton<IQylMcpChatClientBuilder, QylMcpChatClientBuilder>();
        services.AddSingleton<IQylMcpAgentsBuilder, QylMcpAgentsBuilder>();

        var collectorUrl = configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
        services.AddCollectorHttpClient(collectorUrl);

        QylToolManifest.RegisterServices(services, skills);

        services.AddCollectorToolClient<ArtifactTools>();
        services.AddSingleton(TimeProvider.System);

        if (skills.IsEnabled(QylSkillKind.Debug))
        {
            services.AddSingleton<JetBrainsDiscovery>();
            services.AddSingleton<RiderMcpProxy>();

            services.AddSingleton<LspServerDefinitions>();
            services.AddSingleton<LspLanguageMappings>();
            services.AddSingleton<LspServerResolution>();
            services.AddSingleton<LspClientWrapper>();
            services.AddSingleton<WorkspaceEditApplier>();

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

        if (string.IsNullOrWhiteSpace(hostOptions.KeycloakAudience))
        {
            throw new InvalidOperationException(
                $"{McpAuthOptions.KeycloakAuthorityEnvVar} is set but {McpHostOptions.KeycloakAudienceEnvVar} is empty. " +
                "Audience validation must be explicit — refusing to start with any-token-from-realm policy.");
        }

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
                options.Audience = hostOptions.KeycloakAudience;
                options.TokenValidationParameters.ValidateAudience = true;

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger(typeof(QylMcpServiceCollectionExtensions).FullName!);
                        var subject = context.Principal?.FindFirst("sub")?.Value ?? "(unknown)";
                        var audience = context.Principal?.FindFirst("aud")?.Value
                                       ?? hostOptions.KeycloakAudience!;
                        LogTokenValidated(logger, subject, audience);
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger(typeof(QylMcpServiceCollectionExtensions).FullName!);
                        LogAudienceMismatch(logger,
                            context.Exception.GetType().Name,
                            context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnForbidden = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger(typeof(QylMcpServiceCollectionExtensions).FullName!);
                        var subject = context.Principal?.FindFirst("sub")?.Value ?? "(unknown)";
                        LogRoleCheckFailed(logger, subject, context.HttpContext.Request.Path);
                        return Task.CompletedTask;
                    }
                };
            })
            .AddMcp(options =>
            {
                options.ForwardAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                options.Events = new McpAuthenticationEvents
                {
                    OnResourceMetadataRequest = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger(typeof(QylMcpServiceCollectionExtensions).FullName!);
                        LogDiscoveryHit(logger, context.HttpContext.Request.Path);

                        var resourceUrl = hostOptions.ResolvePublicMcpUrl(context.HttpContext.Request);
                        context.ResourceMetadata = new ProtectedResourceMetadata
                        {
                            Resource = resourceUrl,
                            AuthorizationServers = [authority],
                            BearerMethodsSupported = McpAuthMetadata.BearerMethodsSupported,
                            ResourceName = QylServerMetadata.DisplayName,
                            ResourceDocumentation = QylServerMetadata.DocumentationUrl
                        };
                        return Task.CompletedTask;
                    }
                };
            });
    }


    [LoggerMessage(EventId = 4001, Level = LogLevel.Information,
        Message = "OAuth protected-resource metadata discovery hit: {Path}")]
    private static partial void LogDiscoveryHit(ILogger logger, string path);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information,
        Message = "JWT validated: sub={Subject} aud={Audience}")]
    private static partial void LogTokenValidated(ILogger logger, string subject, string audience);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Warning,
        Message = "JWT authentication failed: {ExceptionType}: {ExceptionMessage}")]
    private static partial void LogAudienceMismatch(ILogger logger, string exceptionType, string exceptionMessage);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Warning,
        Message = "JWT authorized but role check failed: sub={Subject} path={Path}")]
    private static partial void LogRoleCheckFailed(ILogger logger, string subject, string path);

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
