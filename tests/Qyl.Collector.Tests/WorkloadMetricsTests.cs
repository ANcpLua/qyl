using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Qyl.Run.Workload;

namespace Qyl.Collector.Tests;

public sealed class WorkloadMetricsTests
{
    [Fact]
    public void TokenUsageRecordsInputAndOutputWithSemanticConventionTags()
    {
        var measurements = new ConcurrentBag<TokenMeasurement>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (instrument.Meter.Name == WorkloadTelemetry.MeterName)
            {
                activeListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var attributes = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                attributes.Add(tag.Key, tag.Value);
            }

            measurements.Add(new TokenMeasurement(instrument, value, attributes));
        });
        listener.Start();

        WorkloadTelemetry.RecordGenAiTokenUsage(
            GenAiSpans.GenAiOperationNameValues.Chat,
            GenAiSpans.GenAiProviderNameValues.Openai,
            "gpt-4.1",
            "gpt-4.1-2025-04-14",
            123,
            45);

        Assert.Collection(
            measurements.OrderBy(static measurement => measurement.Value),
            output => AssertMeasurement(output, 45,
                GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiTokenTypeExample2),
            input => AssertMeasurement(input, 123,
                GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiTokenTypeExample1));
    }

    [Fact]
    public void TokenUsageHistogramUsesRecommendedExplicitBoundaries()
    {
        Assert.Equal(
            [
                1d, 4d, 16d, 64d, 256d, 1_024d, 4_096d, 16_384d, 65_536d, 262_144d,
                1_048_576d, 4_194_304d, 16_777_216d, 67_108_864d
            ],
            WorkloadTelemetry.CreateGenAiTokenUsageBucketBoundaries());
    }

    private static void AssertMeasurement(TokenMeasurement measurement, long value, string tokenType)
    {
        Assert.IsType<Histogram<long>>(measurement.Instrument);
        Assert.Equal(GenAiMetrics.MetricGenAiClientTokenUsage, measurement.Instrument.Name);
        Assert.Equal("{token}", measurement.Instrument.Unit);
        Assert.Equal(value, measurement.Value);
        Assert.Equal(5, measurement.Attributes.Count);
        Assert.Equal(GenAiSpans.GenAiOperationNameValues.Chat,
            measurement.Attributes[GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiOperationName]);
        Assert.Equal(GenAiSpans.GenAiProviderNameValues.Openai,
            measurement.Attributes[GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiProviderName]);
        Assert.Equal(tokenType,
            measurement.Attributes[GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiTokenType]);
        Assert.Equal("gpt-4.1",
            measurement.Attributes[GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiRequestModel]);
        Assert.Equal("gpt-4.1-2025-04-14",
            measurement.Attributes[GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiResponseModel]);
    }

    private sealed record TokenMeasurement(
        Instrument Instrument,
        long Value,
        IReadOnlyDictionary<string, object?> Attributes);
}
