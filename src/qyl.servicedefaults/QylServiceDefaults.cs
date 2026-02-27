using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qyl.ServiceDefaults.Instrumentation;

namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults;

/// <summary>
///     Thin wrappers for generator compatibility.
///     All logic lives in <see cref="QylServiceDefaultsExtensions" />.
/// </summary>
public static partial class QylServiceDefaults
{
    private const string DevLogsScript = """
                                         (function() {
                                             const endpoint = '{{ROUTE}}';
                                             const send = (level, args) => {
                                                 try {
                                                     fetch(endpoint, {
                                                         method: 'POST',
                                                         headers: { 'Content-Type': 'application/json' },
                                                         body: JSON.stringify({ level, message: Array.from(args).map(String).join(' '), timestamp: new Date().toISOString() })
                                                     }).catch(() => {});
                                                 } catch {}
                                             };
                                             ['log', 'info', 'warn', 'error', 'debug', 'trace'].forEach(level => {
                                                 const orig = console[level];
                                                 console[level] = function(...args) { orig.apply(console, args); send(level, args); };
                                             });
                                             console.log('[DevLogs] Frontend logging bridge active');
                                         })();
                                         """;

    /// <summary>
    ///     Thin wrapper for generator compatibility — delegates to <see cref="QylServiceDefaultsExtensions.UseQyl" />.
    /// </summary>
    public static TBuilder TryUseQylConventions<TBuilder>(this TBuilder builder,
        Action<QylServiceDefaultsOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        _ = configure; // Kept for API compatibility — UseQyl() uses QylOptions instead
        builder.UseQyl();
        return builder;
    }

    /// <summary>
    ///     Thin wrapper for generator compatibility — delegates to <see cref="QylServiceDefaultsExtensions.MapQylEndpoints" />.
    /// </summary>
    public static void MapQylDefaultEndpoints(this WebApplication app)
    {
        app.MapQylEndpoints();
    }

    /// <summary>
    ///     Maps the DevLogs frontend logging bridge endpoint.
    ///     Call after <see cref="MapQylDefaultEndpoints" /> to enable browser console capture.
    /// </summary>
    public static void MapDevLogsEndpoint(this IEndpointRouteBuilder app, string routePattern = "/dev-logs")
    {
        app.MapPost(routePattern, static (DevLogEntry entry, ILogger<DevLogEntry> logger) =>
        {
            var logLevel = entry.Level switch
            {
                var l when string.Equals(l, "error", StringComparison.OrdinalIgnoreCase) => LogLevel.Error,
                var l when string.Equals(l, "warn", StringComparison.OrdinalIgnoreCase) => LogLevel.Warning,
                var l when string.Equals(l, "warning", StringComparison.OrdinalIgnoreCase) => LogLevel.Warning,
                var l when string.Equals(l, "debug", StringComparison.OrdinalIgnoreCase) => LogLevel.Debug,
                var l when string.Equals(l, "trace", StringComparison.OrdinalIgnoreCase) => LogLevel.Trace,
                _ => LogLevel.Information
            };
            logger.LogBrowserMessage(logLevel, entry.Message ?? string.Empty);
            return Results.Ok();
        }).ExcludeFromDescription();

        app.MapGet("/dev-logs.js", () =>
        {
            var script = DevLogsScript.Replace("{{ROUTE}}", routePattern, StringComparison.Ordinal);
            return Results.Content(script, "application/javascript");
        }).ExcludeFromDescription();
    }

    [LoggerMessage(Message = "[BROWSER] {Message}")]
    private static partial void LogBrowserMessage(this ILogger logger, LogLevel level, string message);
}

internal sealed record DevLogEntry(string? Level, string? Message);
