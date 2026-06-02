
using Microsoft.Extensions.Diagnostics.ExceptionSummarization;
using Microsoft.Extensions.Diagnostics.Latency;

namespace Qyl.Collector.Telemetry;

public static class TelemetryExtensions
{
    public static void AddQylTelemetry(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddRedaction(static builder => builder.AddQylRedactors());

        services.AddHttpLogging();

        services.AddExceptionSummarizer(static builder =>
        {
            builder.AddHttpProvider();
        });

        services.AddLogging(static logging =>
        {
            logging.EnableEnrichment(static options =>
            {
                options.CaptureStackTraces = false;
                options.IncludeExceptionMessage = false;
                options.MaxStackTraceLength = 2048;
            });

            logging.EnableRedaction();
        });

        services.AddLogEnricher<QylLogEnricher>();
        services.AddLogEnricher<QylRequestEnricher>();

        services.AddApplicationLogEnricher(static options =>
        {
            options.ApplicationName = true;
            options.BuildVersion = true;
            options.EnvironmentName = true;
        });

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
            SemanticAttributeKeys.SessionId,
            "span.count");

        services.AddLatencyContext();

        services.AddAsyncState();
    }

    public static void UseQylTelemetry(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

        app.UseHttpLogging();

        if (!env.IsEnvironment("Testing"))
        {
            var latencyContext = app.ApplicationServices.GetService<ILatencyContext>();
            if (latencyContext is not null)
            {
                app.UseRequestLatencyTelemetry();
            }
        }
    }
}

public static class QylLoggingBuilderExtensions
{
    public static ILoggingBuilder AddQylLogging(this ILoggingBuilder builder, IHostEnvironment environment)
    {
        builder.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.AddFilter("Microsoft.Hosting", LogLevel.Warning);
        builder.AddFilter("System.Net.Http", LogLevel.Warning);
        builder.AddFilter("Grpc", LogLevel.Warning);

        if (environment.IsDevelopment())
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddFilter("qyl", LogLevel.Debug);
            builder.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Debug);
        }
        else
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("qyl", LogLevel.Information);
        }

        return builder;
    }
}
