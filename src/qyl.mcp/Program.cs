using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Protocol;
using qyl.contracts.Attributes;
using qyl.mcp;
using qyl.mcp.Apps.ErrorExplorer;
using qyl.mcp.Landing;
using qyl.mcp.Agents;
using qyl.mcp.Auth;
using qyl.mcp.Scoping;
using qyl.mcp.Skills;
using qyl.mcp.Tools;
using qyl.mcp.Tools.Debug;
using AnalyticsJsonContext = qyl.mcp.Tools.AnalyticsJsonContext;
using AnomalyJsonContext = qyl.mcp.Tools.AnomalyJsonContext;
using LoomMcpJsonContext = Qyl.Contracts.Loom.LoomMcpJsonContext;
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

    var jsonOptions = ConfigureCommonServices(builder.Services, builder.Configuration, skills, scope);

    IServiceProvider? serviceProvider = null;
    ConfigureMcpServer(
        builder.Services,
        skills,
        jsonOptions,
        McpTransportMode.Stdio,
        null,
        () => serviceProvider);

    var host = builder.Build();
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

    var hostOptions = McpHostOptions.FromConfiguration(builder.Configuration, McpTransportMode.Http);
    ApplyPortFallback(builder.WebHost, builder.Configuration);

    var jsonOptions = ConfigureCommonServices(builder.Services, builder.Configuration, skills, scope);
    ConfigureHttpAuthentication(builder.Services, hostOptions);
    builder.Services.AddHealthChecks();

    IServiceProvider? serviceProvider = null;
    ConfigureMcpServer(
        builder.Services,
        skills,
        jsonOptions,
        McpTransportMode.Http,
        hostOptions,
        () => serviceProvider);

    var app = builder.Build();
    serviceProvider = app.Services;

    if (hostOptions.RequiresAuthentication)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    app.MapGet("/", (HttpRequest request) =>
    {
        var accept = request.Headers.Accept.ToString();
        if (accept.ContainsIgnoreCase("text/html"))
            return Results.Content(
                LandingPage.GetHtml(hostOptions.ResolvePublicMcpUrl(request)),
                "text/html; charset=utf-8");

        return Results.Json(CreateManifest(request, hostOptions));
    });
    app.MapGet("/mcp.json", (HttpRequest request) => Results.Json(CreateManifest(request, hostOptions)));
    app.MapGet("/llms.txt", (HttpRequest request) =>
        Results.Text(CreateLlmsText(request, hostOptions), "text/plain; charset=utf-8"));
    app.MapHealthChecks("/healthz", new HealthCheckOptions());

    var endpoint = app.MapMcp(hostOptions.Path);
    // MCP 1.2.0 keeps Streamable HTTP as the default, so legacy SSE endpoints are not
    // mapped by default. Clients should use POST/GET on the same path from `QYL_MCP_PATH`.
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
    // Required by AddStandardResilienceHandler() — registers a no-op redactor provider.
    services.AddRedaction();
    services.AddMcpAuth(configuration);
    services.AddSingleton(scope);

    var collectorUrl = configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
    services.AddCollectorHttpClient(collectorUrl);

    if (skills.IsEnabled(QylSkillKind.Inspect))
    {
        services.AddCollectorToolClient<ReplayTools>();
        services.AddCollectorToolClient<StructuredLogTools>();
        services.AddCollectorToolClient<GenAiTools>();
        services.AddCollectorToolClient<ErrorTools>();
        services.AddCollectorToolClient<ServiceTools>();
        services.AddCollectorToolClient<SpanQueryTools>();
    }

    if (skills.IsEnabled(QylSkillKind.Health))
    {
        services.AddCollectorToolClient<StorageHealthTools>();
    }

    if (skills.IsEnabled(QylSkillKind.Analytics))
    {
        services.AddCollectorToolClient<AnalyticsTools>();
    }

    if (skills.IsEnabled(QylSkillKind.Agent))
    {
        services.AddCollectorToolClient<SummaryTools>();
    }

    if (skills.IsEnabled(QylSkillKind.Anomaly))
    {
        services.AddCollectorToolClient<AnomalyTools>();
    }

    if (skills.IsEnabled(QylSkillKind.Loom))
    {
        services.AddCollectorToolClient<TriageTools>();
        services.AddCollectorToolClient<ExportForAgentTools>();
        services.AddCollectorToolClient<FixTools>();
        services.AddCollectorToolClient<AutofixMcpTools>();
        services.AddCollectorToolClient<RegressionTools>();
        services.AddCollectorToolClient<GitHubMcpTools>();
        services.AddCollectorToolClient<AssistedQueryTools>();
        services.AddCollectorToolClient<TestGenerationTools>();
    }

    services.AddCollectorToolClient<ArtifactTools>();
    services.AddSingleton<RcaTools>();

    services.AddSingleton<McpToolRegistry>();
    services.AddSingleton(TimeProvider.System);

    if (skills.IsEnabled(QylSkillKind.Debug))
    {
        services.AddSingleton<JetBrainsDiscovery>();
        services.AddSingleton<RiderMcpProxy>();
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

static void ConfigureHttpAuthentication(IServiceCollection services, McpHostOptions hostOptions)
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
                        AuthorizationServers = GetAuthorizationServers(authority),
                        BearerMethodsSupported = McpAuthMetadata.BearerMethodsSupported,
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
    var mcpBuilder = services.AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "qyl", Version = ServerVersion.Value };
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
    builder.AppendLine(
        "qyl exposes observability tools for traces, logs, errors, builds, analytics, RCA, and AI workflows.");
    builder.AppendLine();
    builder.AppendLine($"- Endpoint: {hostOptions.ResolvePublicMcpUrl(request)}");
    builder.AppendLine("- Transport: Streamable HTTP");
    builder.AppendLine(
        $"- Auth: {(hostOptions.RequiresAuthentication ? "OAuth 2.0 bearer token" : "No host auth configured")}");
    builder.AppendLine();
    builder.AppendLine(
        "Primary tool families: inspect, health, analytics, agent, build, anomaly, and loom.");
    return builder.ToString();
}

static string[] GetAuthorizationServers(string authority) => [authority];

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

    file static class McpAuthMetadata
    {
        public static readonly string[] BearerMethodsSupported = ["header"];
    }
}
