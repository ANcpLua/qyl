using Microsoft.Extensions.DependencyInjection;

namespace Qyl.Orchestration.Internal;

internal static class OrchestrationServices
{
    internal static void Register(IServiceCollection services, IReadOnlyList<QylResource> resources,
        OrchestratorOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IReadOnlyList<QylResource>>(resources.ToArray());
        services.AddHttpClient(OrchestrationDefaults.HttpClients.HealthProbe, static client =>
                client.Timeout = TimeSpan.FromSeconds(OrchestrationDefaults.Orchestrator.HealthProbeAttemptTimeoutSeconds))
            .RemoveAllLoggers();
        services.AddSingleton<QylResourceRegistry>();
        services.AddSingleton<QylResourceActions>();
        services.AddSingleton<QylLogStore>();
        services.AddSingleton<QylProcessLauncher>();
    }
}
