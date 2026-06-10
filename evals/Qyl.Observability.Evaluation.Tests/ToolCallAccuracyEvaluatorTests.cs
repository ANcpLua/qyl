using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Qyl.Observability.Evaluation.Evaluators;

namespace Qyl.Observability.Evaluation.Tests;

public sealed class ToolCallAccuracyEvaluatorTests
{
    [Fact]
    public void Analyze_MatchesNestedJsonSemantically()
    {
        var record = EvaluatorTestData.Record(
            toolCalls:
            [
                EvaluatorTestData.ToolCall("qyl.query.traces", """{"filter":{"duration_ms":1000,"service":"checkout-api"},"window":"15m"}""")
            ],
            expectedToolCalls:
            [
                EvaluatorTestData.ExpectedToolCall("QYL.QUERY.TRACES", """{"filter":{"service":"checkout-api","duration_ms":1000}}""")
            ]);

        AnalysisResult result = ToolCallAccuracyEvaluator.Analyze(record);

        Assert.True(result.Passed, result.Reason);
    }

    [Fact]
    public void Analyze_FailsWhenJsonValueIsNotSemanticallyEqual()
    {
        var record = EvaluatorTestData.Record(
            toolCalls: [EvaluatorTestData.ToolCall("qyl.query.traces", """{"window":"1h"}""")],
            expectedToolCalls: [EvaluatorTestData.ExpectedToolCall("qyl.query.traces", """{"window":"15m"}""")]);

        AnalysisResult result = ToolCallAccuracyEvaluator.Analyze(record);

        Assert.False(result.Passed);
        Assert.Contains("qyl.query.traces", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_ConsumesEachActualToolCallOnce()
    {
        var record = EvaluatorTestData.Record(
            toolCalls: [EvaluatorTestData.ToolCall("qyl.query.logs", """{"service":"payments-api"}""")],
            expectedToolCalls:
            [
                EvaluatorTestData.ExpectedToolCall("qyl.query.logs", """{"service":"payments-api"}"""),
                EvaluatorTestData.ExpectedToolCall("qyl.query.logs", """{"service":"payments-api"}""")
            ]);

        AnalysisResult result = ToolCallAccuracyEvaluator.Analyze(record);

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsFailedMetricForMismatchedToolCall()
    {
        var record = EvaluatorTestData.Record(
            toolCalls: [EvaluatorTestData.ToolCall("qyl.query.metrics", """{"window":"1h"}""")],
            expectedToolCalls: [EvaluatorTestData.ExpectedToolCall("qyl.query.metrics", """{"window":"15m"}""")]);

        EvaluationResult result = await new ToolCallAccuracyEvaluator(record).EvaluateAsync(
            [],
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "")),
            cancellationToken: TestContext.Current.CancellationToken);

        BooleanMetric metric = result.Get<BooleanMetric>("qyl.tool.call.accuracy");
        Assert.False(metric.Value);
        Assert.True(metric.Interpretation?.Failed);
    }
}
