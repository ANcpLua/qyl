using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using qyl.mcp;
using qyl.mcp.Agents;
using qyl.mcp.Auth;
using qyl.mcp.Tools;
using qyl.mcp.Scoping;
using qyl.mcp.Skills;
using qyl.protocol.Attributes;

var skills = SkillConfiguration.FromEnvironment();
var scope = QylScope.FromEnvironment();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(static o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add MCP authentication support (reads QYL_MCP_TOKEN env var)
// If no token configured, auth is disabled (dev mode)
builder.Services.AddMcpAuth(builder.Configuration);

// Scope narrowing: QYL_SERVICE and QYL_SESSION env vars auto-append to all collector requests
builder.Services.AddSingleton(scope);

// HTTP client for collector API with resilience (retry, circuit breaker)
// Per CLAUDE.md: qyl.mcp → qyl.collector via HTTP ONLY
var collectorUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";

// DI registrations gated by skill config — matches WithSkillTools MCP exposure
if (skills.IsEnabled(QylSkillKind.Inspect))
{
    builder.Services.AddCollectorToolClient<ReplayTools>(collectorUrl);
    builder.Services.AddCollectorToolClient<ConsoleTools>(collectorUrl);
    builder.Services.AddCollectorToolClient<StructuredLogTools>(collectorUrl);
    builder.Services.AddCollectorToolClient<GenAiTools>(collectorUrl);
    builder.Services.AddCollectorToolClient<ErrorTools>(collectorUrl);
    builder.Services.AddCollectorToolClient<ServiceTools>(collectorUrl);
    builder.Services.AddCollectorToolClient<SpanQueryTools>(collectorUrl);
}

if (skills.IsEnabled(QylSkillKind.Health))
{
    builder.Services.AddCollectorToolClient<StorageHealthTools>(collectorUrl);
}

if (skills.IsEnabled(QylSkillKind.Analytics))
{
    builder.Services.AddCollectorToolClient<AnalyticsTools>(collectorUrl);
}

if (skills.IsEnabled(QylSkillKind.Agent))
{
    builder.Services.AddCollectorToolClient<SummaryTools>(collectorUrl);
}

if (skills.IsEnabled(QylSkillKind.Build))
{
    builder.Services.AddCollectorToolClient<BuildTools>(collectorUrl);
}

if (skills.IsEnabled(QylSkillKind.Anomaly))
{
    builder.Services.AddCollectorToolClient<AnomalyTools>(collectorUrl);
}

if (skills.IsEnabled(QylSkillKind.Copilot))
{
    builder.Services.AddCollectorToolClient<CopilotTools>(collectorUrl, TimeSpan.FromSeconds(60));
}

if (skills.IsEnabled(QylSkillKind.ClaudeCode))
{
    builder.Services.AddCollectorToolClient<ClaudeCodeTools>(collectorUrl);
}

// Always registered — used by ITelemetryStore and agent infrastructure
builder.Services.AddCollectorToolClient<HttpTelemetryStore>(collectorUrl);
builder.Services.AddSingleton<RcaTools>();

// Agent provider: proxies to collector's /api/v1/copilot/chat for embedded LLM investigation
builder.Services.AddHttpClient(nameof(HttpAgentProvider), client =>
{
    client.BaseAddress = new Uri(collectorUrl);
    client.Timeout = TimeSpan.FromSeconds(120); // Agent calls may take longer due to tool-calling
}).AddStandardResilienceHandler();
builder.Services.AddSingleton<IAgentProvider>(static sp =>
    new HttpAgentProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpAgentProvider)),
        sp.GetRequiredService<ILogger<HttpAgentProvider>>()));

// McpToolRegistry: discovers all [McpServerTool] methods for use_qyl meta-agent
builder.Services.AddSingleton<McpToolRegistry>();

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
jsonOptions.TypeInfoResolverChain.Add(StorageHealthJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(SpanQueryJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(ReplayJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(CopilotJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(ClaudeCodeMcpJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(AnalyticsJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(ServiceMcpJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(AgentJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(ErrorJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(AnomalyJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(SummaryJsonContext.Default);

// Captured after builder.Build() below; valid when any tool filter lambda runs.
IServiceProvider? sp = null;

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithMessageFilters(filters =>
    {
        filters.AddIncomingFilter(next => async (context, cancellationToken) =>
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
                TelemetryConstants.ActivitySource.StartActivity("mcp.send", ActivityKind.Client, parentContext: default);

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
            string? toolName = request.Params?.Name;

            // Admin role gate — blocks destructive tools when Keycloak is active and role is absent
            if (toolName is not null)
            {
                CallToolResult? denied = sp!.GetRequiredService<McpAdminToolFilter>().CheckAccess(toolName);
                if (denied is not null) return denied;
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
        });
    })
    .WithSkillTools(skills, jsonOptions);

IHost host = builder.Build();
sp = host.Services;
await host.RunAsync().ConfigureAwait(false);

namespace qyl.mcp
{
    file static class Rpc
    {
        public const string System = "rpc.system";
        public const string Method = "rpc.method";
        public const string JsonrpcVersion = "rpc.jsonrpc.version";
        public const string JsonrpcRequestId = "rpc.jsonrpc.request_id";
    }
}
