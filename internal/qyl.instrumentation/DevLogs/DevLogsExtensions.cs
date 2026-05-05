using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Qyl.Instrumentation.DevLogs;

public static partial class DevLogsExtensions
{
    private const string BridgeScript = """
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
            var script = BridgeScript.ReplaceOrdinal("{{ROUTE}}", routePattern);
            return Results.Content(script, "application/javascript");
        }).ExcludeFromDescription();
    }

    [LoggerMessage(Message = "[BROWSER] {Message}")]
    private static partial void LogBrowserMessage(this ILogger logger, LogLevel level, string message);
}

internal sealed record DevLogEntry(string? Level, string? Message);
