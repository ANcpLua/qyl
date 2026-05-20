using System.Collections.Frozen;
using System.Text.Json;
using ANcpLua.Agents.Mcp.Hosting.Authentication;
using ANcpLua.Agents.Mcp.Hosting.Filters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Generated;
using Qyl.Instrumentation.Instrumentation.Mcp;
using qyl.mcp.Apps.ErrorExplorer;
using qyl.mcp.Apps.QueryStudio;
using qyl.mcp.Apps.TraceExplorer;
using qyl.mcp.Auth;
using qyl.mcp.Metadata;
using qyl.mcp.Scoping;

namespace qyl.mcp.Hosting;

internal static class QylMcpServerRegistration
{
    public static void Configure(
        IServiceCollection services,
        SkillConfiguration skills,
        JsonSerializerOptions jsonOptions,
        McpTransportMode transport,
        McpHostOptions? hostOptions)
    {
        services.AddSingleton<IMcpTaskStore>(_ => new InMemoryMcpTaskStore(
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(6),
            TimeSpan.FromSeconds(1),
            maxTasks: 500));

        var builder = services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = QylServerMetadata.Name, Version = QylServerMetadata.Version
            };
            options.ServerInstructions = QylServerMetadata.Instructions;
        });

        services.AddOptions<McpServerOptions>()
            .Configure<IMcpTaskStore>((options, taskStore) => options.TaskStore = taskStore);

        builder = transport switch
        {
            McpTransportMode.Http => builder.WithHttpTransport(options =>
            {
                if (hostOptions is not null)
                    options.Stateless = hostOptions.Stateless;
            }),
            _ => builder.WithStdioServerTransport()
        };

        if (transport is McpTransportMode.Http)
        {
            services.AddAuthorization();
            builder.AddAuthorizationFilters();

            if (hostOptions is { RequiresAuthentication: true })
            {
                if (string.IsNullOrWhiteSpace(hostOptions.KeycloakAudience))
                {
                    throw new InvalidOperationException(
                        $"{McpAuthOptions.KeycloakAuthorityEnvVar} is set but " +
                        $"{McpHostOptions.KeycloakAudienceEnvVar} is empty. " +
                        "Audience validation must be explicit — refusing to start with " +
                        "any-token-from-realm policy.");
                }

                ConfigureOAuth(builder, hostOptions);
            }
        }

        builder
            .UseQylMcpInstrumentation(TelemetryConstants.ActivitySource, options =>
            {
                options.Transport = transport switch
                {
                    McpTransportMode.Http => "http",
                    _ => "stdio"
                };
            })
            .WithQylAdminFilter(options =>
            {
                options.RequiredRole = McpAuthRoles.Admin;
                options.AdminToolNames = QylAdminTools.Names;
            })
            .WithQylScopeInjection<QylScope>()
            .WithAnthropicResultSizeMeta(thresholdChars: 10_000)
            .WithTools<CapabilityTools>(jsonOptions);

        QylToolManifest.RegisterTools(builder, skills, jsonOptions);

        if (skills.IsEnabled(QylSkillKind.Apps))
        {
            builder
                .WithResources<TraceExplorerResource>()
                .WithResources<ErrorExplorerResource>()
                .WithResources([QueryStudioResource.Create()]);
        }
    }

    private static void ConfigureOAuth(IMcpServerBuilder builder, McpHostOptions hostOptions)
    {
        builder.WithQylOAuthProtectedResource(options =>
        {
            options.Authority = hostOptions.KeycloakAuthority!;
            options.Audience = hostOptions.KeycloakAudience!;
            options.ResolveResourceUrl = req => new Uri(hostOptions.ResolvePublicMcpUrl(req));
            options.ConfigureMetadata = metadata =>
            {
                metadata.ResourceName = QylServerMetadata.DisplayName;
                metadata.ResourceDocumentation = new Uri(QylServerMetadata.DocumentationUrl);
            };
            options.ConfigureJwtEvents = events =>
            {
                events.OnTokenValidated = OnTokenValidatedAsync;
                events.OnAuthenticationFailed = OnAuthenticationFailedAsync;
                events.OnForbidden = OnForbiddenAsync;
            };
        });

        static Task OnTokenValidatedAsync(TokenValidatedContext context)
        {
            var logger = CreateLogger(context.HttpContext.RequestServices);
            var subject = context.Principal?.FindFirst("sub")?.Value ?? "(unknown)";
            var audience = context.Principal?.FindFirst("aud")?.Value ?? "(unspecified)";
            logger.LogInformation("JWT validated: sub={Subject} aud={Audience}", subject, audience);
            return Task.CompletedTask;
        }

        static Task OnAuthenticationFailedAsync(AuthenticationFailedContext context)
        {
            var logger = CreateLogger(context.HttpContext.RequestServices);
            logger.LogWarning(
                "JWT authentication failed: {ExceptionType}: {ExceptionMessage}",
                context.Exception.GetType().Name,
                context.Exception.Message);
            return Task.CompletedTask;
        }

        static Task OnForbiddenAsync(ForbiddenContext context)
        {
            var logger = CreateLogger(context.HttpContext.RequestServices);
            var subject = context.Principal?.FindFirst("sub")?.Value ?? "(unknown)";
            logger.LogWarning(
                "JWT authorized but role check failed: sub={Subject} path={Path}",
                subject,
                context.HttpContext.Request.Path.ToString());
            return Task.CompletedTask;
        }

        static ILogger CreateLogger(IServiceProvider services) =>
            services.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(QylMcpServerRegistration).FullName!);
    }
}

internal static class McpAuthRoles
{
    public const string Admin = "qyl:admin";
}

internal static class QylAdminTools
{
    public static readonly IReadOnlySet<string> Names = FrozenSet<string>.Empty;
}
