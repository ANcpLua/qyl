using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using qyl.agents.telemetry;

namespace qyl.sdk.aspnetcore;

public static class QylAspNetCoreExtensions
{
    public static IServiceCollection AddQylAgentObservability(this IServiceCollection services)
    {
        Environment.SetEnvironmentVariable(
            "OTEL_SEMCONV_STABILITY_OPT_IN",
            "gen_ai_latest_experimental");

        var qylSource = new ActivitySource(new ActivitySourceOptions(GenAiAttributes.SourceName)
        {
            Version = "2.0.0",
            TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.38.0"
        });
        services.AddSingleton(qylSource);

        return services;
    }

    public static AIAgentBuilder UseQylOpenTelemetry(
        this AIAgentBuilder builder,
        string? sourceName = null,
        Action<OpenTelemetryAgent>? configure = null)
    {
        string src = sourceName ?? GenAiAttributes.SourceName;
        return builder.UseOpenTelemetry(src, configure);
    }
}
