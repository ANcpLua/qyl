using Microsoft.AspNetCore.Diagnostics;
using Qyl.Collector;
using Qyl.Collector.Dashboard;
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

        // The embedded dashboard is deliberately a local, unsecured development surface. Never
        // expose or accept the ingest-capable API key in browser storage; ApiKey deployments use
        // generated API clients and do not serve the browser UI.
        if (!otlpApiKeyOptions.IsApiKeyMode)
        {
            var webRoot = app.Environment.WebRootPath;
            if (string.IsNullOrEmpty(webRoot))
            {
                var candidate = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                if (Directory.Exists(candidate))
                    webRoot = candidate;
            }

            if (webRoot is { Length: > 0 } && File.Exists(Path.Combine(webRoot, "index.html")))
            {
                app.Environment.WebRootPath = webRoot;
                app.UseDefaultFiles();
                app.UseStaticFiles();
            }
            else if (EmbeddedDashboardExtensions.HasEmbeddedDashboard())
            {
                app.UseEmbeddedDashboard();
            }
        }

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
