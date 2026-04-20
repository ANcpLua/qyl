namespace qyl.mcp.Hosting;

using System.Text.Json;
using Apps.ErrorExplorer;
using Apps.QueryStudio;
using Apps.TraceExplorer;
using Auth;
using contracts.Attributes;
using Metadata;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using Qyl.Generated;
using Scoping;

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
        var mcpBuilder = services.AddMcpServer(options =>
        {
            options.ServerInfo =
                new Implementation { Name = QylServerMetadata.Name, Version = QylServerMetadata.Version };
            options.ServerInstructions = QylServerMetadata.Instructions;
        });

        mcpBuilder = transport switch
        {
            McpTransportMode.Http => mcpBuilder.WithHttpTransport(options =>
            {
                if (hostOptions is not null)
                    options.Stateless = hostOptions.Stateless;
            }),
            _ => mcpBuilder.WithStdioServerTransport()
        };

        if (transport is McpTransportMode.Http)
            mcpBuilder.AddAuthorizationFilters();

        mcpBuilder
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
                    catch (Exception ex)
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        activity?.AddException(ex);
                        throw;
                    }
                });

                filters.AddOutgoingFilter(next => async (context, cancellationToken) =>
                {
                    using var activity =
                        TelemetryConstants.ActivitySource.StartActivity("mcp.send", ActivityKind.Client,
                            parentContext: default);

                    switch (context.JsonRpcMessage)
                    {
                        case JsonRpcResponse response:
                            activity?.SetTag(Rpc.System, "jsonrpc");
                            activity?.SetTag(Rpc.JsonrpcRequestId, response.Id.ToString());
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
                    var toolName = request.Params?.Name;

                    if (toolName is not null)
                    {
                        var denied = serviceProviderAccessor()!
                            .GetRequiredService<McpAdminToolFilter>()
                            .CheckAccess(toolName);

                        if (denied is not null)
                            return denied;
                    }

                    var injectedScope = serviceProviderAccessor()!.GetRequiredService<QylScope>();
                    if (request.Params is not null && injectedScope.HasScope)
                    {
                        request.Params.Arguments = ConstraintInjector.InjectScope(
                            request.Params.Arguments,
                            injectedScope);
                    }

                    using var activity = TelemetryConstants.ActivitySource.StartActivity(
                        toolName is not null
                            ? $"{GenAiAttributes.Operations.ExecuteTool} {toolName}"
                            : GenAiAttributes.Operations.ExecuteTool);

                    activity?.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.ExecuteTool);
                    activity?.SetTag(GenAiAttributes.ToolName, toolName);
                    activity?.SetTag(GenAiAttributes.ToolType, GenAiAttributes.ToolTypes.Extension);
                    activity?.SetTag(Rpc.Method, "tools/call");

                    try
                    {
                        var result = await next(request, cancellationToken);
                        if (result.IsError is true)
                            activity?.SetStatus(ActivityStatusCode.Error, "Tool returned error");

                        var totalChars = result.Content?
                            .OfType<TextContentBlock>()
                            .Sum(static content => content.Text?.Length ?? 0) ?? 0;

                        if (totalChars > 10_000)
                        {
                            result.Meta ??= [];
                            result.Meta["anthropic/maxResultSizeChars"] = totalChars;
                        }

                        return result;
                    }
                    catch (Exception ex)
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        activity?.AddException(ex);
                        throw;
                    }
                });
            })
            .WithTools<CapabilityTools>(jsonOptions);

        QylToolManifest.RegisterTools(mcpBuilder, skills, jsonOptions);

        if (skills.IsEnabled(QylSkillKind.Apps))
        {
            mcpBuilder
                .WithResources<TraceExplorerResource>()
                .WithResources<ErrorExplorerResource>()
                .WithResources([QueryStudioResource.Create()]);
        }
    }
}

file static class Rpc
{
    public const string System = "rpc.system";
    public const string Method = "rpc.method";
    public const string JsonrpcVersion = "rpc.jsonrpc.version";
    public const string JsonrpcRequestId = "rpc.jsonrpc.request_id";
}
