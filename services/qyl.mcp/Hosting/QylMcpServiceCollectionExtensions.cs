using System.Text.Json;
using ANcpLua.Agents.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Generated;
using qyl.mcp.Agents;
using qyl.mcp.Auth;
using qyl.mcp.Clients;
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

/// <summary>
/// DI registrations shared by every qyl-mcp host (stdio dev host + in-process
/// collector HTTP host). Public so the collector can register the same
/// services without duplicating the JSON-context wiring and skill setup.
/// </summary>
public static class QylMcpServiceCollectionExtensions
{
    public static JsonSerializerOptions AddQylMcpCommonServices(
        this IServiceCollection services,
        IConfiguration configuration,
        SkillConfiguration skills,
        QylScope scope)
    {
        services.AddRedaction();
        services.AddHttpContextAccessor();
        services.AddCollectorClientCredentials(configuration);
        services.AddSingleton(skills);
        services.AddSingleton(scope);
        services.AddSingleton<IQylConstraintInjector<QylScope>, QylScopeInjector>();
        services.AddSingleton<CapabilityTools>();

        services.AddSingleton<IQylMcpChatClientBuilder, QylMcpChatClientBuilder>();
        services.AddSingleton<IQylMcpAgentsBuilder, QylMcpAgentsBuilder>();

        var collectorUrl = configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
        services.AddCollectorHttpClient(collectorUrl);

        QylToolManifest.RegisterServices(services, skills);

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
        services.AddSingleton<ITrackerStatsStore, HttpTrackerStatsStore>();

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.TypeInfoResolverChain.Add(TelemetryJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(TelemetryToolsJsonContext.Default);
        jsonOptions.TypeInfoResolverChain.Add(TrackerStatsJsonContext.Default);
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

        return jsonOptions;
    }
}
