using OpenTelemetry.Trace;

namespace qyl.agents.telemetry;

/// <summary>
/// Extension methods for configuring OpenTelemetry tracing for AI agents.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds instrumentation for AI agents to the tracer provider.
    /// Includes Microsoft.Agents.AI and Microsoft.Extensions.AI sources.
    /// </summary>
    public static TracerProviderBuilder AddQylAgentInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddSource("Microsoft.Agents.AI.*")       // Microsoft Agent Framework
            .AddSource("Microsoft.Extensions.AI.*")   // Microsoft Extensions AI
            .AddSource(GenAiSemanticConventions.SourceName); // qyl custom source
    }

    /// <summary>
    /// Adds a custom source for agent instrumentation.
    /// </summary>
    public static TracerProviderBuilder AddAgentSource(
        this TracerProviderBuilder builder,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        return builder.AddSource(sourceName);
    }
}
