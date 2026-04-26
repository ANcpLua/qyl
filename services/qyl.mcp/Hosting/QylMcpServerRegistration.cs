using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Generated;
using qyl.mcp.Apps.ErrorExplorer;
using qyl.mcp.Apps.QueryStudio;
using qyl.mcp.Apps.TraceExplorer;
using qyl.mcp.Auth;
using qyl.mcp.Metadata;
using qyl.mcp.Scoping;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace qyl.mcp.Hosting;

/// <summary>
///     Wires the qyl MCP server into DI: transport (stdio or Streamable HTTP), authorization, JSON-RPC telemetry,
///     and the generator-emitted tool manifest. Telemetry mirrors the MAF <c>Executor</c> pattern — one
///     <c>ActivityKind.Server</c> span per inbound message, child <c>execute_tool {name}</c> spans parented via
///     <c>Activity.Current</c>, W3C trace context propagated through <c>params._meta.traceparent</c> by the
///     <c>ModelContextProtocol</c> SDK. MAF runtime is intentionally NOT imported here — dispatch is owned by the
///     MCP SDK, and layering a MAF <c>Executor</c> on top would double-dispatch every <c>tools/call</c>. qyl.loom
///     is where the MAF workflow runtime actually runs.
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
            .WithMessageFilters(filters =>
            {
                filters.AddIncomingFilter(next => async (context, cancellationToken) =>
                {
                    var method = context.JsonRpcMessage switch
                    {
                        JsonRpcRequest request => request.Method,
                        JsonRpcNotification notification => notification.Method,
                        _ => null
                    };

                    using var activity = TelemetryConstants.ActivitySource.StartActivity(
                        method is not null ? $"mcp.receive {method}" : "mcp.receive",
                        ActivityKind.Server);

                    if (method is not null)
                    {
                        activity?.SetTag(Rpc.System, "jsonrpc");
                        activity?.SetTag(Rpc.Method, method);
                        activity?.SetTag(Rpc.JsonrpcVersion, "2.0");
                    }

                    try
                    {
                        await next(context, cancellationToken);
                    }
                    catch (Exception ex) when (RecordAndPropagate(activity, ex))
                    {
                        throw; // unreachable — RecordAndPropagate returns false, exception escapes the filter
                    }
                });

                filters.AddOutgoingFilter(next => async (context, cancellationToken) =>
                {
                    using var activity = TelemetryConstants.ActivitySource.StartActivity(
                        "mcp.send",
                        ActivityKind.Client);

                    switch (context.JsonRpcMessage)
                    {
                        case JsonRpcResponse response:
                            activity?.SetTag(Rpc.System, "jsonrpc");
                            activity?.SetTag(Rpc.JsonrpcRequestId, response.Id.ToString());
                            break;
                        case JsonRpcRequest request:
                            activity?.SetTag(Rpc.System, "jsonrpc");
                            activity?.SetTag(Rpc.Method, request.Method);
                            activity?.SetTag(Rpc.JsonrpcRequestId, request.Id.ToString());
                            break;
                        case JsonRpcNotification notification:
                            activity?.SetTag(Rpc.System, "jsonrpc");
                            activity?.SetTag(Rpc.Method, notification.Method);
                            break;
                    }

                    await next(context, cancellationToken);
                });
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

                    using var activity = TelemetryConstants.ActivitySource.StartActivity(
                        $"{GenAiAttributes.OperationNameValues.ExecuteTool} {toolName}");

                    activity?.SetTag(GenAiAttributes.OperationName, GenAiAttributes.OperationNameValues.ExecuteTool);
                    activity?.SetTag(GenAiAttributes.ToolName, toolName);
                    activity?.SetTag(GenAiAttributes.ToolType, "extension");
                    activity?.SetTag(Rpc.Method, "tools/call");

                    try
                    {
                        var result = await next(request, cancellationToken);

                        if (result.IsError is true)
                            activity?.SetStatus(ActivityStatusCode.Error, "Tool returned error");

                        var totalChars = result.Content
                            .OfType<TextContentBlock>()
                            .Sum(static content => content.Text.Length);

                        if (totalChars > 10_000)
                        {
                            result.Meta ??= [];
                            result.Meta["anthropic/maxResultSizeChars"] = totalChars;
                        }

                        return result;
                    }
                    catch (Exception ex) when (RecordAndPropagate(activity, ex))
                    {
                        throw; // unreachable — RecordAndPropagate returns false, exception escapes the filter
                    }
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

    // Observe-only exception recorder for the MCP transport boundary. Returning false from a `when`
    // filter means the catch clause never runs — the exception propagates with its original stack
    // trace preserved. Narrowing the catch type would leave unknown exception kinds un-recorded on
    // the span, violating the OTel contract that server spans carry ActivityStatusCode.Error on any
    // unhandled failure. The breadth is intentional and strictly scoped to this transport layer.
    private static bool RecordAndPropagate(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddException(ex);
        return false;
    }
}

file static class Rpc
{
    public const string System = "rpc.system.name";
    public const string Method = "rpc.method";
    public const string JsonrpcVersion = "jsonrpc.protocol.version";
    public const string JsonrpcRequestId = "jsonrpc.request.id";
}
