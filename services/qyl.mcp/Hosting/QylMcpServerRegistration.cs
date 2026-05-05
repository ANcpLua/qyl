using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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
        McpHostOptions? hostOptions,
        Func<IServiceProvider?> serviceProviderAccessor)
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
            builder.AddAuthorizationFilters();

        builder
            .UseQylMcpInstrumentation(TelemetryConstants.ActivitySource, options =>
            {
                options.Transport = transport switch
                {
                    McpTransportMode.Http => "http",
                    _ => "stdio"
                };
            })
            .WithRequestFilters(filters =>
            {
                filters.AddCallToolFilter(next => async (request, cancellationToken) =>
                {
                    var toolName = request.Params.Name;
                    var scopedServices = serviceProviderAccessor()!;

                    var denied = scopedServices
                        .GetRequiredService<McpAdminToolFilter>()
                        .CheckAccess(toolName);
                    if (denied is not null)
                        return denied;

                    var scope = scopedServices.GetRequiredService<QylScope>();
                    if (scope.HasScope)
                    {
                        request.Params.Arguments = ConstraintInjector.InjectScope(
                            request.Params.Arguments,
                            scope);
                    }

                    var result = await next(request, cancellationToken);

                    var totalChars = result.Content
                        .OfType<TextContentBlock>()
                        .Sum(static content => content.Text.Length);

                    if (totalChars > 10_000)
                    {
                        result.Meta ??= [];
                        result.Meta["anthropic/maxResultSizeChars"] = totalChars;
                    }

                    return result;
                });
            })
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
}
