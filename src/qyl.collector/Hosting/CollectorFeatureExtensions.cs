using Qyl.Collector.Analytics;
using Qyl.Collector.Autofix;
using Qyl.Collector.Dashboards;
using Qyl.Collector.Identity;
using Qyl.Collector.Provisioning;
using Qyl.Collector.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenAI;
using System.ClientModel;

namespace Qyl.Collector.Hosting;

public static class CollectorFeatureExtensions
{
    public static IServiceCollection AddQylCollectorFeatures(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Loom triage + autofix
        services.AddSingleton<TriagePipelineService>();
        services.AddHostedService(static sp => sp.GetRequiredService<TriagePipelineService>());
        services.AddSingleton<AutofixAgentService>();
        services.AddHostedService(static sp => sp.GetRequiredService<AutofixAgentService>());

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

        // Error issue engine + lifecycle + autofix
        services.AddSingleton<IssueService>();
        services.AddSingleton<ErrorLifecycleService>();
        services.AddSingleton<AutofixOrchestrator>();
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
        services.TryAddSingleton<IChatClient?>(BuildLoomChatClient);

        // Search
        services.AddSingleton<SearchService>();

        // Auto-generated dashboards
        services.AddDashboardServices();

        return services;
    }

    private static IChatClient? BuildLoomChatClient(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();

        var apiKey = config["QYL_AGENT_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var model = config["QYL_AGENT_MODEL"] ?? "gpt-4o";
        var endpoint = config["QYL_AGENT_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint))
            return new OpenAIClient(apiKey).GetChatClient(model).AsIChatClient();

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
            throw new InvalidOperationException($"QYL_AGENT_ENDPOINT '{endpoint}' is not a valid absolute URI.");

        var options = new OpenAIClientOptions { Endpoint = endpointUri };
        return new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(model).AsIChatClient();
    }
}
