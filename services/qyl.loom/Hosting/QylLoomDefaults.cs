
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

            // Registration tables declared as locals (not class-level statics) — the C# 14
            // `extension` block analyzer doesn't propagate references from inside the block
            // back to class-level private members, so static readonly arrays at class level
            // would all trip IDE0052. Startup allocation cost is once per process.
            (Type Interface, Type Implementation)[] threeBuilderInterfaces =
            [
                (typeof(IQylLoomChatClientBuilder), typeof(QylLoomChatClientBuilder)),
                (typeof(IQylLoomAgentsBuilder), typeof(QylLoomAgentsBuilder)),
                (typeof(IQylLoomWorkflowBuilder), typeof(QylLoomWorkflowBuilder)),
            ];

            Type[] autofixInfrastructure =
            [
                typeof(AutofixReportAssemblyState),
                typeof(AutofixRunRegistry),
                typeof(AutofixContextLoader),
                typeof(AutofixContextTools),
                typeof(AutofixRunConfigStore),
                typeof(AutofixWorkflowFactory),
                typeof(AutofixOrchestrator),
                typeof(LoomAutofixRunner),
            ];

            (Type Interface, Type Implementation)[] autofixKeyedInterfaces =
            [
                (typeof(IAutofixStepLedger), typeof(CollectorAutofixStepLedger)),
                (typeof(IAutofixLifecycleBus), typeof(InMemoryAutofixLifecycleBus)),
            ];

            Type[] explorationServices =
            [
                typeof(ExplorationContextBuilder),
                typeof(ExplorationSessionStore),
                typeof(ExplorationDiagnostician),
                typeof(ExplorationStrategist),
                typeof(ExplorationInsightService),
                typeof(ExplorationOrchestrator),
            ];

            // HTTP clients — the CollectorClient lambda captures `builder` for configuration
            // lookup so it intentionally is NOT `static`. The GitHub handler does not capture
            // and IS `static`.
            builder.Services.AddHttpClient<CollectorClient>(client =>
            {
                var baseUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddStandardResilienceHandler();

            builder.Services.AddHttpClient("GitHub", static client =>
            {
                client.BaseAddress = new Uri("https://api.github.com");
                client.DefaultRequestHeaders.Add("User-Agent", "qyl-loom");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            }).AddStandardResilienceHandler();

            foreach (var (iface, impl) in threeBuilderInterfaces)
                builder.Services.AddSingleton(iface, impl);

            foreach (var t in autofixInfrastructure)
                builder.Services.AddSingleton(t);

            foreach (var (iface, impl) in autofixKeyedInterfaces)
                builder.Services.AddSingleton(iface, impl);

            // Checkpoint persistence — the second registration captures the first via factory.
            builder.Services.AddSingleton<FileSystemAutofixCheckpointStore>();
            builder.Services.AddSingleton(static sp =>
                CheckpointManager.CreateJson(sp.GetRequiredService<FileSystemAutofixCheckpointStore>()));

            foreach (var t in explorationServices)
                builder.Services.AddSingleton(t);

            builder.Services.AddSingleton<CodeReviewService>();

            return builder;
        }
    }
}
