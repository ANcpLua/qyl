using OpenTelemetry.Trace;

namespace qyl.collector;

public static class TracerProviderBuilderExtensions
{
    extension(TracerProviderBuilder builder)
    {
        public TracerProviderBuilder AddQylAgentInstrumentation()
        {
            Throw.IfNull(builder);
            return builder
                .AddSource("Microsoft.Agents.AI.*")
                .AddSource("Microsoft.Extensions.AI.*")
                .AddSource(GenAiAttributes.SourceName);
        }

        public TracerProviderBuilder AddAgentSource(string sourceName)
        {
            Throw.IfNull(builder);
            Throw.IfNullOrEmpty(sourceName);
            return builder.AddSource(sourceName);
        }
    }
}
