// qyl OpenTelemetry ASP.NET Core Example
// Custom ActivitySource and Meter holder for manual instrumentation

namespace qyl.AspNetCore.Example;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Holds references to ActivitySource and Meter for manual instrumentation.
/// Register as singleton in DI to share across controllers.
/// </summary>
public sealed class InstrumentationSource : IDisposable
{
    internal const string ActivitySourceName = "qyl.AspNetCore.Example";
    internal const string MeterName = "qyl.AspNetCore.Example";

    private readonly Meter _meter;

    public InstrumentationSource()
    {
        var version = typeof(InstrumentationSource).Assembly.GetName().Version?.ToString();
        ActivitySource = new ActivitySource(ActivitySourceName, version);
        _meter = new Meter(MeterName, version);

        // Custom metrics for the example
        FreezingDaysCounter = _meter.CreateCounter<long>(
            "weather.days.freezing",
            description: "The number of days where the temperature is below freezing");

        RequestCounter = _meter.CreateCounter<long>(
            "qyl.requests.count",
            description: "Total number of weather forecast requests");
    }

    /// <summary>
    /// ActivitySource for creating manual spans.
    /// </summary>
    public ActivitySource ActivitySource { get; }

    /// <summary>
    /// Counter for freezing days in forecasts.
    /// </summary>
    public Counter<long> FreezingDaysCounter { get; }

    /// <summary>
    /// Counter for total requests.
    /// </summary>
    public Counter<long> RequestCounter { get; }

    public void Dispose()
    {
        ActivitySource.Dispose();
        _meter.Dispose();
    }
}
