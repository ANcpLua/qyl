// =============================================================================
// qyl Telemetry DI Extensions - .NET 10 Full Setup
// Configures logging enrichment, redaction, buffering, HTTP logging
// =============================================================================

namespace qyl.collector.Telemetry;

/// <summary>
///     Extension methods for configuring qyl telemetry in DI.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    ///     Adds full qyl telemetry configuration.
    ///     Includes: enrichment, redaction, buffering.
    /// </summary>
    public static IServiceCollection AddQylTelemetry(
        this IServiceCollection services)
    {
        // HTTP context for request enrichment
        services.AddHttpContextAccessor();

        // Configure redaction
        services.AddRedaction(static builder => builder.AddQylRedactors());

        // HTTP logging for incoming requests (redaction applied via AddRedaction above)
        services.AddHttpLogging();

        // Exception summarization for safe error logging
        services.AddExceptionSummarizer(static builder =>
        {
            builder.AddHttpProvider();
        });

        // Configure logging with enrichment, redaction, and buffering
        services.AddLogging(static logging =>
        {
            // Enable log enrichment
            logging.EnableEnrichment(options =>
            {
                options.CaptureStackTraces = true;
                options.IncludeExceptionMessage = true;
                options.MaxStackTraceLength = 2048; // Min allowed by validator
            });

            // Enable redaction in logs
            logging.EnableRedaction();

            // .NET 9+ Log buffering - buffer Debug logs, flush on exception
            logging.AddGlobalBuffer(options =>
            {
                options.Rules.Add(new LogBufferingFilterRule(logLevel: LogLevel.Debug));
                options.Rules.Add(new LogBufferingFilterRule(logLevel: LogLevel.Trace));
            });
        });

        // Add enrichers
        services.AddLogEnricher<QylLogEnricher>();
        services.AddLogEnricher<QylRequestEnricher>();

        // Add application log enricher (built-in)
        services.AddApplicationLogEnricher(static options =>
        {
            options.ApplicationName = true;
            options.BuildVersion = true;
            options.EnvironmentName = true;
        });

        // Register latency monitoring checkpoints
        services.RegisterCheckpointNames(
            "db.query",
            "span.ingest",
            "span.store",
            "session.query",
            "genai.extract");

        services.RegisterMeasureNames(
            "ingestion.duration",
            "query.duration",
            "storage.duration");

        services.RegisterTagNames(
            "session.id",
            "gen_ai.provider.name",
            "gen_ai.request.model",
            "span.count");

        // Add latency context
        services.AddLatencyContext();

        // AsyncState for request-scoped context across async boundaries
        services.AddAsyncState();
        services.AddLogEnricher<SpanContextEnricher>();

        return services;
    }

    /// <summary>
    ///     Configures the application pipeline with telemetry middleware.
    ///     Skips latency telemetry in Production with CreateSlimBuilder to avoid service registration issues.
    /// </summary>
    public static IApplicationBuilder UseQylTelemetry(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

        // HTTP logging with redaction
        app.UseHttpLogging();

        // Skip latency telemetry in testing environment or when ILatencyContext isn't available
        // WebApplicationFactory with CreateSlimBuilder doesn't properly register all telemetry services
        if (!env.IsEnvironment("Testing"))
        {
            // Check if latency context is actually registered before using middleware
            var latencyContext = app.ApplicationServices.GetService<ILatencyContext>();
            if (latencyContext is not null)
            {
                app.UseRequestLatencyTelemetry();
            }
        }

        return app;
    }
}

/// <summary>
///     Logging builder extensions for qyl-specific configuration.
/// </summary>
public static class QylLoggingBuilderExtensions
{
    /// <summary>
    ///     Configures minimal production logging.
    /// </summary>
    public static ILoggingBuilder AddQylProductionLogging(this ILoggingBuilder builder)
    {
        builder.SetMinimumLevel(LogLevel.Information);

        // Suppress noisy framework logs
        builder.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.AddFilter("Microsoft.Hosting", LogLevel.Warning);
        builder.AddFilter("System.Net.Http", LogLevel.Warning);
        builder.AddFilter("Grpc", LogLevel.Warning);

        // Keep qyl logs at Info
        builder.AddFilter("qyl", LogLevel.Information);

        return builder;
    }

    /// <summary>
    ///     Configures development logging with more detail.
    /// </summary>
    public static ILoggingBuilder AddQylDevelopmentLogging(this ILoggingBuilder builder)
    {
        builder.SetMinimumLevel(LogLevel.Debug);

        // More verbose for development
        builder.AddFilter("qyl", LogLevel.Debug);
        builder.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Debug);

        return builder;
    }
}
