using OpenTelemetry.Trace;

namespace qyl.collector;

public static class TracerProviderBuilderExtensions
{
    public static TracerProviderBuilder AddQylAgentInstrumentation(this TracerProviderBuilder builder)
    {
        Throw.IfNull(builder);
        return builder
            .AddSource("Microsoft.Agents.AI.*")
            .AddSource("Microsoft.Extensions.AI.*")
            .AddSource(GenAiAttributes.SourceName);
    }

    public static TracerProviderBuilder AddAgentSource(
        this TracerProviderBuilder builder,
        string sourceName)
    {
        Throw.IfNull(builder);
        Throw.IfNullOrEmpty(sourceName);
        return builder.AddSource(sourceName);
    }
}
