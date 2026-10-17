using Microsoft.AspNetCore.Diagnostics;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Telemetry;

namespace Qyl.Collector.Hosting;

internal static class CollectorMiddlewareExtensions
{
    public static WebApplication UseQylCollectorMiddleware(this WebApplication app)
    {
        var otlpCorsOptions = app.Services.GetRequiredService<OtlpCorsOptions>();
        var otlpApiKeyOptions = app.Services.GetRequiredService<OtlpApiKeyOptions>();

        if (otlpCorsOptions.IsEnabled)
            app.UseMiddleware<OtlpCorsMiddleware>(otlpCorsOptions);

        app.UseMiddleware<CollectorApiKeyMiddleware>(otlpApiKeyOptions);

        app.UseExceptionHandler(static errorApp =>
        {
            errorApp.Run(WriteUnhandledExceptionAsync);
        });

        app.UseQylTelemetry();

        return app;
    }

    internal static async Task WriteUnhandledExceptionAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Qyl.Collector.ExceptionHandler");
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (exceptionFeature?.Error is { } error)
        {
            ExceptionHandlerLog.UnhandledException(
                logger,
                error,
                HttpTelemetryNames.NormalizeMethod(context.Request.Method));
        }

        context.Response.Headers["X-Trace-Id"] = traceId;
        await ContractErrorResults.WriteInternalServerErrorAsync(
            context.Response,
            "collector.unhandled_exception").ConfigureAwait(false);
    }
}

internal static partial class ExceptionHandlerLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method}")]
    public static partial void UnhandledException(ILogger logger, Exception exception, string method);
}
