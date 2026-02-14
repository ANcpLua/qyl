using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using qyl.mcp;
using qyl.mcp.Auth;
using qyl.mcp.Tools;
using qyl.protocol.Attributes;
using qyl.protocol.Attributes.Generated;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(static o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add MCP authentication support (reads QYL_MCP_TOKEN env var)
// If no token configured, auth is disabled (dev mode)
builder.Services.AddMcpAuth(builder.Configuration);

// HTTP client for collector API with resilience (retry, circuit breaker)
// Per CLAUDE.md: qyl.mcp → qyl.collector via HTTP ONLY
var collectorUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";

builder.Services.AddCollectorToolClient<ReplayTools>(collectorUrl);
builder.Services.AddCollectorToolClient<HttpTelemetryStore>(collectorUrl);
builder.Services.AddCollectorToolClient<ConsoleTools>(collectorUrl);
builder.Services.AddCollectorToolClient<StructuredLogTools>(collectorUrl);
builder.Services.AddCollectorToolClient<BuildTools>(collectorUrl);
builder.Services.AddCollectorToolClient<GenAiTools>(collectorUrl);
builder.Services.AddCollectorToolClient<StorageTools>(collectorUrl);
builder.Services.AddCollectorToolClient<CopilotTools>(collectorUrl, TimeSpan.FromSeconds(60));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITelemetryStore>(static sp =>
    new HttpTelemetryStore(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpTelemetryStore)),
        sp.GetRequiredService<TimeProvider>(),
        sp.GetRequiredService<ILogger<HttpTelemetryStore>>()));

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.TypeInfoResolverChain.Add(TelemetryJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(ConsoleJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(LogsJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(BuildJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(GenAiJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(StorageJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(ReplayJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(CopilotJsonContext.Default);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .AddIncomingMessageFilter(next => async (context, cancellationToken) =>
    {
        var method = context.JsonRpcMessage switch
        {
            JsonRpcRequest req => req.Method,
            JsonRpcNotification notif => notif.Method,
            _ => null
        };

        using var activity = TelemetryConstants.ActivitySource.StartActivity(
            method is not null ? $"mcp.receive {method}" : "mcp.receive",
            ActivityKind.Server);

        if (method is not null)
        {
            activity?.SetTag(RpcSystemAttributes.System, "jsonrpc");
            activity?.SetTag(RpcMethodAttributes.Method, method);
            activity?.SetTag(RpcJsonrpcAttributes.Version, "2.0");
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
    })
    .AddOutgoingMessageFilter(next => async (context, cancellationToken) =>
    {
        using var activity = TelemetryConstants.ActivitySource.StartActivity("mcp.send", ActivityKind.Client, parentContext: default);

        switch (context.JsonRpcMessage)
        {
            case JsonRpcResponse response:
                activity?.SetTag(RpcSystemAttributes.System, "jsonrpc");
                activity?.SetTag(RpcJsonrpcAttributes.RequestId, response.Id.ToString());
                break;
            case JsonRpcNotification notification:
                activity?.SetTag(RpcSystemAttributes.System, "jsonrpc");
                activity?.SetTag(RpcMethodAttributes.Method, notification.Method);
                break;
        }

        await next(context, cancellationToken);
    })
    .AddCallToolFilter(next => async (request, cancellationToken) =>
    {
        var toolName = request.Params?.Name;

        using var activity = TelemetryConstants.ActivitySource.StartActivity(
            toolName is not null
                ? $"{GenAiAttributes.Operations.ExecuteTool} {toolName}"
                : GenAiAttributes.Operations.ExecuteTool);

        activity?.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.ExecuteTool);
        activity?.SetTag(GenAiAttributes.ToolName, toolName);
        activity?.SetTag(GenAiAttributes.ToolType, GenAiAttributes.ToolTypes.Extension);
        activity?.SetTag(RpcMethodAttributes.Method, "tools/call");

        try
        {
            var result = await next(request, cancellationToken);
            if (result.IsError is true)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Tool returned error");
            }
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    })
    .WithTools<TelemetryTools>(jsonOptions)
    .WithTools<ReplayTools>(jsonOptions)
    .WithTools<ConsoleTools>(jsonOptions)
    .WithTools<StructuredLogTools>(jsonOptions)
    .WithTools<BuildTools>(jsonOptions)
    .WithTools<GenAiTools>(jsonOptions)
    .WithTools<StorageTools>(jsonOptions)
    .WithTools<CopilotTools>(jsonOptions);

await builder.Build().RunAsync().ConfigureAwait(false);
