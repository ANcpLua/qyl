using System.Diagnostics.Metrics;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.GenAi;
using ErrorAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Error.ErrorAttributes;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Collector.Tests.Telemetry;

public sealed class GenAiMetricsTests
{
    [Fact]
    public async Task ExecuteAsync_Records_Token_And_Duration_Metrics_Without_Activity_Listener()
    {
        using var collector = new InProcessMetricCollector(IsGenAiRuntimeMetric);

        var result = await GenAiInstrumentation.ExecuteAsync(
            "openai",
            GenAiAttributes.OperationNameValues.Chat,
            "gpt-5.5",
            static () => Task.FromResult("ok"),
            static _ => new TokenUsage(InputTokens: 11, OutputTokens: 7));

        result.Should().Be("ok");

        var measurements = collector.Measurements;
        var inputTokens = measurements.Should().ContainSingle(static measurement =>
            measurement.Name == "gen_ai.client.token.usage" &&
            measurement.Value == 11d &&
            measurement.HasTag(GenAiAttributes.TokenType, GenAiAttributes.TokenTypeValues.Input)).Subject;
        AssertCommonGenAiTags(inputTokens);

        var outputTokens = measurements.Should().ContainSingle(static measurement =>
            measurement.Name == "gen_ai.client.token.usage" &&
            measurement.Value == 7d &&
            measurement.HasTag(GenAiAttributes.TokenType, GenAiAttributes.TokenTypeValues.Output)).Subject;
        AssertCommonGenAiTags(outputTokens);

        var duration = measurements.Should().ContainSingle(static measurement =>
            measurement.Name == "gen_ai.client.operation.duration").Subject;
        duration.Unit.Should().Be("s");
        duration.Description.Should().Be("Operation duration");
        duration.Value.Should().BeGreaterThanOrEqualTo(0d);
        AssertCommonGenAiTags(duration);
    }

    [Fact]
    public async Task ExecuteAsync_Records_Error_Duration_Metric_Without_Activity_Listener()
    {
        using var collector = new InProcessMetricCollector(IsGenAiRuntimeMetric);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(static () =>
            GenAiInstrumentation.ExecuteAsync<string>(
                "openai",
                GenAiAttributes.OperationNameValues.Chat,
                "gpt-5.5",
                static () => Task.FromException<string>(new InvalidOperationException("boom"))));

        exception.Message.Should().Be("boom");

        var duration = collector.Measurements.Should().ContainSingle(static measurement =>
            measurement.Name == "gen_ai.client.operation.duration" &&
            measurement.HasTag(ErrorAttributes.Type, nameof(InvalidOperationException))).Subject;
        duration.Value.Should().BeGreaterThanOrEqualTo(0d);
        AssertCommonGenAiTags(duration);
    }

    private static bool IsGenAiRuntimeMetric(Instrument instrument) =>
        instrument.Meter.Name == ActivitySources.GenAi &&
        instrument.Name is "gen_ai.client.token.usage" or "gen_ai.client.operation.duration";

    private static void AssertCommonGenAiTags(CapturedMetricMeasurement measurement)
    {
        measurement.MeterName.Should().Be(ActivitySources.GenAi);
        measurement.HasTag(GenAiAttributes.OperationName, GenAiAttributes.OperationNameValues.Chat)
            .Should().BeTrue();
        measurement.HasTag(GenAiAttributes.ProviderName, "openai").Should().BeTrue();
        measurement.HasTag(GenAiAttributes.RequestModel, "gpt-5.5").Should().BeTrue();
    }
}
