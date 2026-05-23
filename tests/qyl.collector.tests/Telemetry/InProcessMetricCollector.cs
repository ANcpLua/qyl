using System.Diagnostics.Metrics;
using System.Globalization;

namespace Qyl.Collector.Tests.Telemetry;

internal sealed class InProcessMetricCollector : IDisposable
{
    private readonly Lock _gate = new();
    private readonly List<CapturedMetricMeasurement> _measurements = [];
    private readonly MeterListener _listener;

    public InProcessMetricCollector(Func<Instrument, bool> shouldListen)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (shouldListen(instrument))
                    listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<int>(Capture);
        _listener.SetMeasurementEventCallback<long>(Capture);
        _listener.SetMeasurementEventCallback<byte>(Capture);
        _listener.SetMeasurementEventCallback<short>(Capture);
        _listener.SetMeasurementEventCallback<float>(Capture);
        _listener.SetMeasurementEventCallback<double>(Capture);
        _listener.SetMeasurementEventCallback<decimal>(Capture);
        _listener.Start();
    }

    public IReadOnlyList<CapturedMetricMeasurement> Measurements
    {
        get
        {
            lock (_gate)
                return new List<CapturedMetricMeasurement>(_measurements);
        }
    }

    public void RecordObservableInstruments() =>
        _listener.RecordObservableInstruments();

    public void Dispose() =>
        _listener.Dispose();

    private void Capture<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
        where T : struct, IConvertible
    {
        var capturedTags = new List<CapturedMetricTag>(tags.Length);
        foreach (var tag in tags)
            capturedTags.Add(new CapturedMetricTag(tag.Key, tag.Value));

        var captured = new CapturedMetricMeasurement(
            instrument.Meter.Name,
            instrument.Name,
            instrument.Unit,
            instrument.Description,
            measurement.ToDouble(CultureInfo.InvariantCulture),
            capturedTags);

        lock (_gate)
            _measurements.Add(captured);
    }
}

internal sealed record CapturedMetricMeasurement(
    string MeterName,
    string Name,
    string? Unit,
    string? Description,
    double Value,
    IReadOnlyList<CapturedMetricTag> Tags)
{
    public bool HasTag(string key, object? value)
    {
        foreach (var tag in Tags)
        {
            if (string.Equals(tag.Key, key, StringComparison.Ordinal) &&
                Equals(tag.Value, value))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed record CapturedMetricTag(string Key, object? Value);
