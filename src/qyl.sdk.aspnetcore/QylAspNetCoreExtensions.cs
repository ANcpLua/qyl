using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using qyl.agents.telemetry;

namespace qyl.sdk.aspnetcore;

public static class QylAspNetCoreExtensions
{
    /// <summary>
    /// Adds Qyl GenAI observability to ASP.NET Core:
    /// - Enables trace ingestion (Activity events)
    /// - Enables OTEL semantic conventions for GenAI v1.38
    /// - Sets latest-experimental mode for GenAI semantics
    /// </summary>
    public static IServiceCollection AddQylAgentObservability(this IServiceCollection services)
    {
        // Ensure the environment variable for 1.38 semantics is set
        Environment.SetEnvironmentVariable(
            "OTEL_SEMCONV_STABILITY_OPT_IN",
            "gen_ai_latest_experimental");

        // ActivitySource for qyl pipeline
        var qylSource = new ActivitySource(GenAiSemanticConventions.SourceName);
        services.AddSingleton(qylSource);

        return services;
    }

    /// <summary>
    /// Convenience wrapper around <see cref="Microsoft.Agents.AI.AIAgentBuilderExtensions.UseOpenTelemetry"/>
    /// that defaults the source name to QYL's source.
    /// </summary>
    public static AIAgentBuilder UseQylOpenTelemetry(
        this AIAgentBuilder builder,
        string? sourceName = null,
        Action<OpenTelemetryAgent>? configure = null)
    {
        var src = sourceName ?? GenAiSemanticConventions.SourceName;
        return builder.UseOpenTelemetry(src, configure);
    }
}
