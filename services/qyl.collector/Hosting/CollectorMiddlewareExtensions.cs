using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
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
                    ExceptionHandlerLog.UnhandledException(logger, context.Request.Method, error);
                }

                await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error", traceId });
            });
        });

        // Keycloak JWT auth is registered only when configured (see AddQylCollectorAuth). Gate the
        // middleware on the scheme provider so an unconfigured (dev) collector doesn't fail at startup.
        if (app.Services.GetService<IAuthenticationSchemeProvider>() is not null)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

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
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method}")]
    public static partial void UnhandledException(ILogger logger, string method, Exception error);
}
