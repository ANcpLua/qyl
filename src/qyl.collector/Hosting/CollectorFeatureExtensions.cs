using Qyl.Collector.Analytics;
using Qyl.Collector.Autofix;
using Qyl.Collector.Dashboards;
using Qyl.Collector.Identity;
using Qyl.Collector.Provisioning;
using Qyl.Collector.Search;

namespace Qyl.Collector.Hosting;

public static class CollectorFeatureExtensions
{
    public static IServiceCollection AddQylCollectorFeatures(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Identity + workspace
        services.AddSingleton<WorkspaceService>();
        services.AddSingleton<HandshakeService>();
        services.AddSingleton<ProjectService>();

        // GitHub identity integration (ADR-002)
        services.AddSingleton<GitHubService>();
        services.AddHttpClient("GitHub", client =>
        {
            client.BaseAddress = new Uri(config.GetValue("GitHub:BaseAddress", "https://api.github.com/") ??
                                         "https://api.github.com/");
            client.DefaultRequestHeaders.Add("User-Agent", "qyl/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        }).AddStandardResilienceHandler();

        // Provisioning: profiles + code generation
        services.AddSingleton<ProfileService>();
        services.AddSingleton<GenerationProfileService>();
        services.AddSingleton<GenerationJobService>();

        // Anomaly detection + log summary
        services.AddSingleton<AnomalyService>();
        services.AddSingleton<LogSummaryService>();

        // Error issue engine + lifecycle
        services.AddSingleton<IssueService>();
        services.AddSingleton<ErrorLifecycleService>();
        services.AddSingleton<PrCreationService>();
        services.AddSingleton<AgentHandoffService>();

        // Loom code review + debugging
        services.AddSingleton<CodeReviewService>();
        services.AddSingleton<LoomInsightService>();
        services.AddSingleton<IssueContextBuilder>();
        services.AddSingleton<LoomSessionStore>();
        services.AddSingleton<LoomOrchestrator>();
        services.AddKeyedSingleton<LoomDiagnostician>(LoomAgentKeys.Diagnostician);
        services.AddKeyedSingleton<LoomStrategist>(LoomAgentKeys.Strategist);

        // Search
        services.AddSingleton<SearchService>();

        // Auto-generated dashboards
        services.AddDashboardServices();

        return services;
    }
}
