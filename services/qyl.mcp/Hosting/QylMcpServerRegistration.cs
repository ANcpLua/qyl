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

/// <summary>
///     Wires the qyl MCP server into DI: transport (stdio or Streamable HTTP), authorization, the qyl telemetry
///     facade, and the generator-emitted tool manifest. Telemetry is delegated to
///     <see cref="QylMcpServerInstrumentation.UseQylMcpInstrumentation" /> — the facade emits one
///     <c>ActivityKind.Server</c> span per inbound JSON-RPC message and child spans for <c>tools/call</c>,
///     <c>resources/read</c>, and <c>prompts/get</c>, parented via <c>Activity.Current</c>; W3C trace context
///     flows through <c>params._meta.traceparent</c> as supplied by the SDK. The remaining inline filter handles
///     business concerns (admin tool denial, scope injection, anthropic max-result-size meta) that have nothing
///     to do with telemetry. MAF runtime is intentionally NOT imported here — dispatch is owned by the MCP SDK,
///     and layering a MAF <c>Executor</c> on top would double-dispatch every <c>tools/call</c>. qyl.loom is where
///     the MAF workflow runtime actually runs.
/// </summary>
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
        // Shared task store for tools declared with TaskSupport = Required|Optional.
        // Singleton — the MCP SDK uses it to persist long-running task state across
        // tools/call turns (GET resumption, SSE reconnect, cancellation).
        // TTLs + limits chosen for a single-node dev/prod deployment; tune per profile.
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

        // Wire the task store onto the server options via the DI-built singleton, so
        // one instance is shared between McpServerOptions.TaskStore and any tool that
        // resolves IMcpTaskStore through DI.
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
            .UseQylMcpInstrumentation(TelemetryConstants.ActivitySource)
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
