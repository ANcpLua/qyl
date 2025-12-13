namespace qyl.AspNetCore.Example;

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

        FreezingDaysCounter = _meter.CreateCounter<long>(
            "weather.days.freezing",
            description: "The number of days where the temperature is below freezing");

        RequestCounter = _meter.CreateCounter<long>(
            "qyl.requests.count",
            description: "Total number of weather forecast requests");
    }

    public ActivitySource ActivitySource { get; }

    public Counter<long> FreezingDaysCounter { get; }

    public Counter<long> RequestCounter { get; }

    public void Dispose()
    {
        ActivitySource.Dispose();
        _meter.Dispose();
    }
}