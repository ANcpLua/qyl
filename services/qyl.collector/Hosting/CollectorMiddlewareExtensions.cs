using Microsoft.AspNetCore.Diagnostics;
using Qyl.Collector.Dashboard;
using Qyl.Collector.Telemetry;

namespace Qyl.Collector.Hosting;

public static class CollectorMiddlewareExtensions
{
    public static WebApplication UseQylCollectorMiddleware(this WebApplication app)
    {
        var otlpCorsOptions = app.Services.GetRequiredService<OtlpCorsOptions>();
        var otlpApiKeyOptions = app.Services.GetRequiredService<OtlpApiKeyOptions>();

        if (otlpCorsOptions.IsEnabled)
            app.UseMiddleware<OtlpCorsMiddleware>(otlpCorsOptions);

        app.UseMiddleware<OtlpApiKeyMiddleware>(otlpApiKeyOptions);

        app.UseRequestDecompression();

        app.UseExceptionHandler(static errorApp =>
        {
            errorApp.Run(static async context =>
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Qyl.Collector.ExceptionHandler");
                var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                if (exceptionFeature?.Error is { } error)
                {
                    ExceptionHandlerLog.UnhandledException(logger, context.Request.Method,
                        context.Request.Path, context.Request.QueryString.ToString(), error);
                }

                await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error", traceId });
            });
        });

        app.UseQylTelemetry();

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

        return app;
    }
}

internal static partial class ExceptionHandlerLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path}{Query}")]
    public static partial void UnhandledException(ILogger logger, string method, string path, string query,
        Exception error);
}
