// =============================================================================
// Qyl.Agents - Service Extensions
// DI registration for LLM providers and agent telemetry
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Qyl.Agents.Instrumentation;
using Qyl.Agents.Providers;
using Qyl.Instrumentation.Instrumentation;

namespace Qyl.Agents;

/// <summary>
///     Extension methods for registering Qyl.Agents services.
/// </summary>
public static class AgentServiceExtensions
{
    /// <summary>
    ///     Adds qyl agent services to the service collection.
    ///     Registers the LLM provider (IChatClient) based on configuration.
    /// </summary>
    public static IServiceCollection AddQylAgents(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Guard.NotNull(services);
        Guard.NotNull(configuration);

        // Register LLM provider options and IChatClient
        var llmOptions = LlmProviderFactory.BindOptions(configuration);
        services.AddSingleton(llmOptions);

        // BYOK client — always registered so visitors can provide their own API key
        services.AddHttpClient("qyl-llm-byok");

        // GitHub Models client — automatic free fallback using user's GitHub token
        services.AddHttpClient("qyl-llm-github-models");

        if (llmOptions.IsConfigured)
        {
            services.AddHttpClient("qyl-llm").AddStandardResilienceHandler();
            services.AddSingleton<IChatClient>(sp =>
            {
                var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpFactory.CreateClient("qyl-llm");
                return LlmProviderFactory.Create(llmOptions, httpClient)
                       ?? throw new InvalidOperationException("LLM provider configured but factory returned null.");
            });
        }

        return services;
    }

    /// <summary>
    ///     Adds qyl agent tracing instrumentation to OpenTelemetry.
    /// </summary>
    public static TracerProviderBuilder AddQylAgentInstrumentation(this TracerProviderBuilder builder)
    {
        Guard.NotNull(builder);

        return builder
            .AddSource(CopilotInstrumentation.SourceName)
            .AddSource(ActivitySources.GenAi);
    }

    /// <summary>
    ///     Adds qyl agent metrics to OpenTelemetry.
    /// </summary>
    public static MeterProviderBuilder AddQylAgentMetrics(this MeterProviderBuilder builder)
    {
        Guard.NotNull(builder);

        return builder.AddMeter(CopilotInstrumentation.MeterName);
    }

    /// <summary>
    ///     Adds qyl agent telemetry (both tracing and metrics) to the service collection.
    /// </summary>
    public static IServiceCollection AddQylAgentTelemetry(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.AddOpenTelemetry()
            .WithTracing(static builder => builder
                .AddSource(CopilotInstrumentation.SourceName)
                .AddSource(ActivitySources.GenAi))
            .WithMetrics(static builder => builder.AddMeter(CopilotInstrumentation.MeterName));

        return services;
    }
}
