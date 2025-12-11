using qyl.AspNetCore.Example.Models;

namespace qyl.AspNetCore.Example.Logging;

internal static partial class LoggerExtensions
{
    private static readonly Func<ILogger, string, IDisposable?> _scope =
        LoggerMessage.DefineScope<string>("{CorrelationId}");

    public static IDisposable? BeginIdScope(this ILogger logger, string id) =>
        _scope(logger, id);

    [LoggerMessage(EventId = 1, Message = "WeatherForecasts generated {Count}: {Forecasts}")]
    public static partial void WeatherForecastGenerated(
        this ILogger logger,
        LogLevel logLevel,
        int count,
        WeatherForecast[] forecasts);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Request received for {Days} days forecast")]
    public static partial void RequestReceived(this ILogger logger, int days);
}
