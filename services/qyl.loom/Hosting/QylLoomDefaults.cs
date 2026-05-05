
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qyl.Loom.Agents;
using Qyl.Loom.Autofix;
using Qyl.Loom.Autofix.Workflow;
using Qyl.Loom.Clients;
using Qyl.Loom.CodeReview;
using Qyl.Loom.Exploration;
using Qyl.Loom.Workflows;
using AutofixOrchestrator = Qyl.Loom.Autofix.AutofixOrchestrator;

namespace Qyl.Loom.Hosting;

public static class QylLoomDefaults
{
    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        public TBuilder AddQylLoomDefaults()
        {
            Guard.NotNull(builder);

            AddHttpClients(builder);
            AddThreeBuilderPattern(builder.Services);
            AddAutofixInfrastructure(builder.Services);
            AddCheckpointPersistence(builder.Services);
            AddExplorationServices(builder.Services);
            AddCodeReviewService(builder.Services);

            return builder;
        }
    }

    private static void AddHttpClients<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHttpClient<CollectorClient>(client =>
        {
            var baseUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        }).AddStandardResilienceHandler();

        builder.Services.AddHttpClient("GitHub", client =>
        {
            client.BaseAddress = new Uri("https://api.github.com");
            client.DefaultRequestHeaders.Add("User-Agent", "qyl-loom");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        }).AddStandardResilienceHandler();
    }

    private static void AddThreeBuilderPattern(IServiceCollection services)
    {
        services.AddSingleton<IQylLoomChatClientBuilder, QylLoomChatClientBuilder>();
        services.AddSingleton<IQylLoomAgentsBuilder, QylLoomAgentsBuilder>();
        services.AddSingleton<IQylLoomWorkflowBuilder, QylLoomWorkflowBuilder>();
    }

    private static void AddAutofixInfrastructure(IServiceCollection services)
    {
        services.AddSingleton<AutofixReportAssemblyState>();
        services.AddSingleton<AutofixRunRegistry>();
        services.AddSingleton<AutofixContextLoader>();
        services.AddSingleton<AutofixContextTools>();
        services.AddSingleton<IAutofixStepLedger, CollectorAutofixStepLedger>();
        services.AddSingleton<IAutofixLifecycleBus, InMemoryAutofixLifecycleBus>();
        services.AddSingleton<AutofixRunConfigStore>();
        services.AddSingleton<AutofixWorkflowFactory>();

        services.AddSingleton<AutofixOrchestrator>();
        services.AddSingleton<LoomAutofixRunner>();
    }

    private static void AddCheckpointPersistence(IServiceCollection services)
    {
        services.AddSingleton<FileSystemAutofixCheckpointStore>();
        services.AddSingleton(sp =>
            CheckpointManager.CreateJson(sp.GetRequiredService<FileSystemAutofixCheckpointStore>()));
    }

    private static void AddExplorationServices(IServiceCollection services)
    {
        services.AddSingleton<ExplorationContextBuilder>();
        services.AddSingleton<ExplorationSessionStore>();
        services.AddSingleton<ExplorationDiagnostician>();
        services.AddSingleton<ExplorationStrategist>();
        services.AddSingleton<ExplorationInsightService>();
        services.AddSingleton<ExplorationOrchestrator>();
    }

    private static void AddCodeReviewService(IServiceCollection services) =>
        services.AddSingleton<CodeReviewService>();
}
