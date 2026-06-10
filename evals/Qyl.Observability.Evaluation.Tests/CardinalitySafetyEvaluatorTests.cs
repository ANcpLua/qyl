using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Qyl.Observability.Evaluation.Evaluators;

namespace Qyl.Observability.Evaluation.Tests;

public sealed class CardinalitySafetyEvaluatorTests
{
    [Fact]
    public void Analyze_PassesSafeBoundedAttributes()
    {
        var record = EvaluatorTestData.Record(
            telemetry: [EvaluatorTestData.Span("span", "trace-1", "span-1", attributesJson: """{"http.route":"/checkout","error.type":"TimeoutException"}""")]);

        AnalysisResult result = CardinalitySafetyEvaluator.Analyze(record);

        Assert.True(result.Passed, result.Reason);
    }

    [Fact]
    public void Analyze_FailsBlockedAttributeKey()
    {
        var record = EvaluatorTestData.Record(
            telemetry: [EvaluatorTestData.Span("span", "trace-1", "span-1", attributesJson: """{"qyl.prompt.raw":"diagnose alice@example.com"}""")]);

        AnalysisResult result = CardinalitySafetyEvaluator.Analyze(record);

        Assert.False(result.Passed);
        Assert.Contains("span:qyl.prompt.raw", result.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"user.id":"alice@example.com"}""")]
    [InlineData("""{"auth.header":"Bearer token"}""")]
    [InlineData("""{"openai.key":"sk-test"}""")]
    public void Analyze_FailsSensitiveAttributeValues(string attributesJson)
    {
        var record = EvaluatorTestData.Record(
            telemetry: [EvaluatorTestData.Span("span", "trace-1", "span-1", attributesJson: attributesJson)]);

        AnalysisResult result = CardinalitySafetyEvaluator.Analyze(record);

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsFailedMetricForUnsafeAttribute()
    {
        var record = EvaluatorTestData.Record(
            telemetry: [EvaluatorTestData.Span("span", "trace-1", "span-1", attributesJson: """{"message.raw":"secret"}""")]);

        EvaluationResult result = await new CardinalitySafetyEvaluator(record).EvaluateAsync(
            [],
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "")),
            cancellationToken: TestContext.Current.CancellationToken);

        BooleanMetric metric = result.Get<BooleanMetric>("qyl.cardinality.safety");
        Assert.False(metric.Value);
        Assert.True(metric.Interpretation?.Failed);
    }
}
