// qyl OpenTelemetry ASP.NET Core Example
// Source-generated logging extensions for high-performance structured logging

namespace qyl.AspNetCore.Example.Logging;

using qyl.AspNetCore.Example.Models;

internal static partial class LoggerExtensions
{
    private static readonly Func<ILogger, string, IDisposable?> Scope =
        LoggerMessage.DefineScope<string>("{CorrelationId}");

    /// <summary>
    /// Begins a logging scope with a correlation ID.
    /// </summary>
    public static IDisposable? BeginIdScope(this ILogger logger, string id) => Scope(logger, id);

    /// <summary>
    /// Logs weather forecast generation with structured data.
    /// </summary>
    [LoggerMessage(EventId = 1, Message = "WeatherForecasts generated {Count}: {Forecasts}")]
    public static partial void WeatherForecastGenerated(
        this ILogger logger,
        LogLevel logLevel,
        int count,
        WeatherForecast[] forecasts);

    /// <summary>
    /// Logs a request received event.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Request received for {Days} days forecast")]
    public static partial void RequestReceived(this ILogger logger, int days);
}
