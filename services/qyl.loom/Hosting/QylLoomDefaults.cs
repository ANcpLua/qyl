// Copyright (c) 2025-2026 ancplua

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

/// <summary>
///     qyl.loom domain DI bundle. Replaces the verbose AddSingleton block in
///     <c>Program.cs</c>. Single entry point per the Option A plan, with the seven
///     concerns split into private named methods so future contributors can scan
///     scope without spelunking the full registration list.
/// </summary>
/// <remarks>
///     Concerns covered:
///     <list type="bullet">
///         <item>Outbound HTTP clients (collector + GitHub) with standard resilience.</item>
///         <item>Three-builder pattern (chat-client → agents → workflow).</item>
///         <item>Autofix workflow infrastructure (run state, registry, ledger, factory).</item>
///         <item>Checkpoint persistence (file-backed JSON store).</item>
///         <item>Background pipeline orchestrators (autofix orchestrator + runner).</item>
///         <item>Exploration services (interactive investigation).</item>
///         <item>Code review service.</item>
///     </list>
/// </remarks>
public static class QylLoomDefaults
{
    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        /// <summary>
        ///     Wire every loom-domain singleton + the two HTTP clients in one call.
        ///     Returns the builder so it composes after <c>AddQylServiceDefaults()</c>.
        /// </summary>
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
        // chat-client → agents → workflow. Every AIAgent and every Workflow flows
        // through these singletons so the .AsBuilder().UseQylAgentTelemetry().Build()
        // wrap is centralized and the workflow topology is constructed once.
        services.AddSingleton<IQylLoomChatClientBuilder, QylLoomChatClientBuilder>();
        services.AddSingleton<IQylLoomAgentsBuilder, QylLoomAgentsBuilder>();
        services.AddSingleton<IQylLoomWorkflowBuilder, QylLoomWorkflowBuilder>();
    }

    private static void AddAutofixInfrastructure(IServiceCollection services)
    {
        // Per-run state, run registry, step ledger, lifecycle bus, workflow factory.
        // All singleton; per-run state keyed by runId.
        services.AddSingleton<AutofixReportAssemblyState>();
        services.AddSingleton<AutofixRunRegistry>();
        services.AddSingleton<AutofixContextLoader>();
        services.AddSingleton<AutofixContextTools>();
        services.AddSingleton<IAutofixStepLedger, CollectorAutofixStepLedger>();
        services.AddSingleton<IAutofixLifecycleBus, InMemoryAutofixLifecycleBus>();
        services.AddSingleton<AutofixRunConfigStore>();
        services.AddSingleton<AutofixWorkflowFactory>();

        // Background pipelines — TriagePipelineService, AutofixAgentService, and
        // RegressionDetectionService auto-register via [QylHostedService] through
        // the generator's QylGeneratedRegistry.RegisterQylHostedServices hook.
        services.AddSingleton<AutofixOrchestrator>();
        services.AddSingleton<LoomAutofixRunner>();
    }

    private static void AddCheckpointPersistence(IServiceCollection services)
    {
        // File-backed JsonCheckpointStore so workflow runs survive process restart
        // and dashboard refresh. Root path configurable via QYL_AUTOFIX_CHECKPOINT_ROOT;
        // otherwise falls under the OS temp dir.
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
