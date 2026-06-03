
using Microsoft.Extensions.Diagnostics.ExceptionSummarization;
using Microsoft.Extensions.Diagnostics.Latency;

namespace Qyl.Collector.Telemetry;

internal static class TelemetryExtensions
{
    public static void AddQylTelemetry(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddRedaction(static builder => builder.AddQylRedactors());

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
            QylLatencyNames.Checkpoints.DbQuery,
            QylLatencyNames.Checkpoints.SpanIngest,
            QylLatencyNames.Checkpoints.SpanStore,
            QylLatencyNames.Checkpoints.SessionQuery,
            QylLatencyNames.Checkpoints.GenAiExtract);

        services.RegisterMeasureNames(
            QylLatencyNames.Measures.IngestionDuration,
            QylLatencyNames.Measures.QueryDuration,
            QylLatencyNames.Measures.StorageDuration);

        services.RegisterTagNames(QylLatencyNames.Tags.SpanCount);

        services.AddRequestLatencyTelemetry();
        services.AddLatencyContext();

        services.AddAsyncState();
    }

    public static void UseQylTelemetry(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

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

internal static class QylLoggingBuilderExtensions
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
