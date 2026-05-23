using System.Diagnostics.Metrics;

namespace Qyl.Collector.Tests.Telemetry;

public sealed class InProcessMetricCollectorTests
{
    [Fact]
    public void Collector_Captures_All_Numeric_Types_Supported_By_Qyl_Meter_Generator()
    {
        var meterName = string.Concat("qyl.tests.numeric.", Guid.NewGuid().ToString("N"));
        using var collector = new InProcessMetricCollector(instrument =>
            string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal));
        using var meter = new Meter(meterName);

        var byteCounter = meter.CreateCounter<byte>("test.byte");
        var shortHistogram = meter.CreateHistogram<short>("test.short");
        var intCounter = meter.CreateCounter<int>("test.int");
        var longCounter = meter.CreateCounter<long>("test.long");
        var floatHistogram = meter.CreateHistogram<float>("test.float");
        var doubleHistogram = meter.CreateHistogram<double>("test.double");
        var decimalGauge = meter.CreateObservableGauge("test.decimal", static () => 8.25m);

        byteCounter.Add(1);
        shortHistogram.Record(2);
        intCounter.Add(3);
        longCounter.Add(4);
        floatHistogram.Record(5.5f);
        doubleHistogram.Record(6.75d);
        collector.RecordObservableInstruments();

        var measurements = collector.Measurements;
        measurements.Should().Contain(static measurement => measurement.Name == "test.byte" && measurement.Value == 1d);
        measurements.Should().Contain(static measurement => measurement.Name == "test.short" && measurement.Value == 2d);
        measurements.Should().Contain(static measurement => measurement.Name == "test.int" && measurement.Value == 3d);
        measurements.Should().Contain(static measurement => measurement.Name == "test.long" && measurement.Value == 4d);
        measurements.Should().Contain(static measurement => measurement.Name == "test.float" && measurement.Value == 5.5d);
        measurements.Should().Contain(static measurement => measurement.Name == "test.double" && measurement.Value == 6.75d);
        measurements.Should().Contain(static measurement => measurement.Name == "test.decimal" && measurement.Value == 8.25d);
        _ = decimalGauge;
    }
}
