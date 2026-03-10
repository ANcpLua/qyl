using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Protocol;
using qyl.mcp;
using qyl.mcp.Agents;
using qyl.mcp.Auth;
using qyl.mcp.Providers;
using qyl.mcp.Scoping;
using qyl.mcp.Skills;
using qyl.mcp.Tools;
using qyl.protocol.Attributes;

var skills = SkillConfiguration.FromEnvironment();
var scope = QylScope.FromEnvironment();
var transport = McpHostOptions.ResolveTransport(args);

if (transport is McpTransportMode.Http)
{
    await RunHttpAsync(args, skills, scope).ConfigureAwait(false);
}
else
{
    await RunStdioAsync(args, skills, scope).ConfigureAwait(false);
}

static async Task RunStdioAsync(
    string[] args,
    SkillConfiguration skills,
    QylScope scope)
{
    var builder = Host.CreateApplicationBuilder(args);
    ConfigureLogging(builder.Logging);

    JsonSerializerOptions jsonOptions = ConfigureCommonServices(builder.Services, builder.Configuration, skills, scope);

    IServiceProvider? serviceProvider = null;
    ConfigureMcpServer(
        builder.Services,
        skills,
        jsonOptions,
        transport: McpTransportMode.Stdio,
        hostOptions: null,
        serviceProviderAccessor: () => serviceProvider);

    IHost host = builder.Build();
    serviceProvider = host.Services;
    await host.RunAsync().ConfigureAwait(false);
}

static async Task RunHttpAsync(
    string[] args,
    SkillConfiguration skills,
    QylScope scope)
{
    var builder = WebApplication.CreateBuilder(args);
    ConfigureLogging(builder.Logging);

    McpHostOptions hostOptions = McpHostOptions.FromConfiguration(builder.Configuration, McpTransportMode.Http);
    ApplyPortFallback(builder.WebHost, builder.Configuration);

    JsonSerializerOptions jsonOptions = ConfigureCommonServices(builder.Services, builder.Configuration, skills, scope);
    ConfigureHttpAuthentication(builder.Services, hostOptions);
    builder.Services.AddHealthChecks();

    IServiceProvider? serviceProvider = null;
    ConfigureMcpServer(
        builder.Services,
        skills,
        jsonOptions,
        transport: McpTransportMode.Http,
        hostOptions: hostOptions,
        serviceProviderAccessor: () => serviceProvider);

    WebApplication app = builder.Build();
    serviceProvider = app.Services;

    if (hostOptions.RequiresAuthentication)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    app.MapGet("/", (HttpRequest request) => Results.Json(CreateManifest(request, hostOptions)));
    app.MapGet("/mcp.json", (HttpRequest request) => Results.Json(CreateManifest(request, hostOptions)));
    app.MapGet("/llms.txt", (HttpRequest request) =>
        Results.Text(CreateLlmsText(request, hostOptions), "text/plain; charset=utf-8"));
    app.MapHealthChecks("/healthz", new HealthCheckOptions());

    var endpoint = app.MapMcp(hostOptions.Path);
    if (hostOptions.RequiresAuthentication)
        endpoint.RequireAuthorization();

    await app.RunAsync().ConfigureAwait(false);
}

static void ConfigureLogging(ILoggingBuilder logging) =>
    logging.AddConsole(static options => options.LogToStandardErrorThreshold = LogLevel.Trace);

static JsonSerializerOptions ConfigureCommonServices(
    IServiceCollection services,
    IConfiguration configuration,
    SkillConfiguration skills,
    QylScope scope)
{
    services.AddRedaction();

    // Collector-facing authentication: qyl.mcp -> qyl.collector
    services.AddMcpAuth(configuration);
    services.AddSingleton(scope);

    string collectorUrl = configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";

    if (skills.IsEnabled(QylSkillKind.Inspect))
    {
        services.AddCollectorToolClient<ReplayTools>(collectorUrl);
        services.AddCollectorToolClient<ConsoleTools>(collectorUrl);
        services.AddCollectorToolClient<StructuredLogTools>(collectorUrl);
        services.AddCollectorToolClient<GenAiTools>(collectorUrl);
        services.AddCollectorToolClient<ErrorTools>(collectorUrl);
        services.AddCollectorToolClient<ServiceTools>(collectorUrl);
        services.AddCollectorToolClient<SpanQueryTools>(collectorUrl);
    }

    if (skills.IsEnabled(QylSkillKind.Health))
    {
        services.AddCollectorToolClient<StorageHealthTools>(collectorUrl);
    }

    if (skills.IsEnabled(QylSkillKind.Analytics))
    {
        services.AddCollectorToolClient<AnalyticsTools>(collectorUrl);
    }

    if (skills.IsEnabled(QylSkillKind.Agent))
    {
        services.AddCollectorToolClient<SummaryTools>(collectorUrl);
    }

    if (skills.IsEnabled(QylSkillKind.Build))
    {
        services.AddCollectorToolClient<BuildTools>(collectorUrl);
    }

    if (skills.IsEnabled(QylSkillKind.Anomaly))
    {
        services.AddCollectorToolClient<AnomalyTools>(collectorUrl);
    }

    if (skills.IsEnabled(QylSkillKind.Copilot))
    {
        services.AddCollectorToolClient<CopilotTools>(collectorUrl, TimeSpan.FromSeconds(60));
    }

    if (skills.IsEnabled(QylSkillKind.ClaudeCode))
    {
        services.AddCollectorToolClient<ClaudeCodeTools>(collectorUrl);
    }

    if (skills.IsEnabled(QylSkillKind.Loom))
    {
        services.AddCollectorToolClient<TriageTools>(collectorUrl);
        services.AddCollectorToolClient<ExportForAgentTools>(collectorUrl);
        services.AddCollectorToolClient<FixTools>(collectorUrl);
        services.AddCollectorToolClient<AutofixMcpTools>(collectorUrl);
        services.AddCollectorToolClient<RegressionTools>(collectorUrl);
        services.AddCollectorToolClient<GitHubMcpTools>(collectorUrl);
        services.AddCollectorToolClient<AgentHandoffTools>(collectorUrl);
        services.AddCollectorToolClient<AssistedQueryTools>(collectorUrl);
        services.AddCollectorToolClient<TestGenerationTools>(collectorUrl);
    }

    services.AddCollectorToolClient<HttpTelemetryStore>(collectorUrl);
    services.AddSingleton<RcaTools>();

    services.AddHttpClient(nameof(HttpAgentProvider), client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(120);
    }).AddStandardResilienceHandler();

    services.AddSingleton<IAgentProvider>(static serviceProvider =>
        new HttpAgentProvider(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpAgentProvider)),
            serviceProvider.GetRequiredService<ILogger<HttpAgentProvider>>()));

    services.AddSingleton<McpToolRegistry>();
    services.AddSingleton(TimeProvider.System);

    // LLM provider: reads QYL_LLM_* env vars, creates provider-agnostic IChatClient
    var llmOptions = LlmProviderFactory.BindOptions(configuration);
    if (llmOptions.IsConfigured)
    {
        services.AddHttpClient("LlmProvider");
        services.AddSingleton<IChatClient>(sp =>
            LlmProviderFactory.Create(
                llmOptions,
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("LlmProvider"))
            ?? throw new InvalidOperationException("LLM provider was configured but factory returned null."));
    }
    services.AddSingleton<ITelemetryStore>(static serviceProvider =>
        new HttpTelemetryStore(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpTelemetryStore)),
            serviceProvider.GetRequiredService<TimeProvider>(),
            serviceProvider.GetRequiredService<ILogger<HttpTelemetryStore>>()));

    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    jsonOptions.TypeInfoResolverChain.Add(TelemetryToolsJsonContext.Default);
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
    jsonOptions.TypeInfoResolverChain.Add(FixToolsJsonContext.Default);

    return jsonOptions;
}

static void ConfigureHttpAuthentication(IServiceCollection services, McpHostOptions hostOptions)
{
    services.AddAuthorization();

    if (!hostOptions.RequiresAuthentication)
        return;

    string authority = hostOptions.KeycloakAuthority!;

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
                        BearerMethodsSupported = ["header"],
                        ResourceName = "qyl MCP",
                        ResourceDocumentation =
                            "https://github.com/ANcpLua/qyl/tree/main/src/qyl.mcp#readme"
                    };
                    return Task.CompletedTask;
                }
            };
        });
}

static void ConfigureMcpServer(
    IServiceCollection services,
    SkillConfiguration skills,
    JsonSerializerOptions jsonOptions,
    McpTransportMode transport,
    McpHostOptions? hostOptions,
    Func<IServiceProvider?> serviceProviderAccessor)
{
    string version = ServerVersion.Value;

    var mcpBuilder = services.AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "qyl",
            Version = version
        };
        options.ServerInstructions =
            "Use qyl tools to inspect telemetry, traces, logs, errors, builds, and AI workflow health.";
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

                if (toolName is not null)
                {
                    CallToolResult? denied = serviceProviderAccessor()!
                        .GetRequiredService<McpAdminToolFilter>()
                        .CheckAccess(toolName);

                    if (denied is not null)
                        return denied;
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
                    CallToolResult result = await next(request, cancellationToken);
                    if (result.IsError is true)
                        activity?.SetStatus(ActivityStatusCode.Error, "Tool returned error");

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
}

static void ApplyPortFallback(ConfigureWebHostBuilder webHost, IConfiguration configuration)
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

static object CreateManifest(HttpRequest request, McpHostOptions hostOptions) => new
{
    name = "qyl",
    version = ServerVersion.Value,
    endpoint = hostOptions.ResolvePublicMcpUrl(request),
    transport = "streamable-http",
    auth = hostOptions.RequiresAuthentication ? "oauth2-bearer" : "none"
};

static string CreateLlmsText(HttpRequest request, McpHostOptions hostOptions)
{
    var builder = new StringBuilder();
    builder.AppendLine("# qyl MCP Server");
    builder.AppendLine();
    builder.AppendLine("qyl exposes observability tools for traces, logs, errors, builds, analytics, RCA, and AI workflows.");
    builder.AppendLine();
    builder.AppendLine($"- Endpoint: {hostOptions.ResolvePublicMcpUrl(request)}");
    builder.AppendLine("- Transport: Streamable HTTP");
    builder.AppendLine($"- Auth: {(hostOptions.RequiresAuthentication ? "OAuth 2.0 bearer token" : "No host auth configured")}");
    builder.AppendLine();
    builder.AppendLine("Primary tool families: inspect, health, analytics, agent, build, anomaly, copilot, Claude Code, and loom.");
    return builder.ToString();
}


namespace qyl.mcp
{
    file static class ServerVersion
    {
        public static readonly string Value =
            Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.1.0-beta";
    }

    file static class Rpc
    {
        public const string System = "rpc.system";
        public const string Method = "rpc.method";
        public const string JsonrpcVersion = "rpc.jsonrpc.version";
        public const string JsonrpcRequestId = "rpc.jsonrpc.request_id";
    }
}
