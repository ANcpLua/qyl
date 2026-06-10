using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Qyl.Observability.Evaluation.Evaluators;

namespace Qyl.Observability.Evaluation.Tests;

public sealed class TraceCorrelationEvaluatorTests
{
    [Fact]
    public void Analyze_PassesWhenParentResolvesInsideSameTrace()
    {
        var record = EvaluatorTestData.Record(
            telemetry:
            [
                EvaluatorTestData.Span("root", "trace-1", "span-root"),
                EvaluatorTestData.Span("child", "trace-1", "span-child", "span-root")
            ]);

        AnalysisResult result = TraceCorrelationEvaluator.Analyze(record);

        Assert.True(result.Passed, result.Reason);
    }

    [Fact]
    public void Analyze_FailsMissingTraceIdEvenWhenSpanIdsWouldCollide()
    {
        var record = EvaluatorTestData.Record(
            telemetry:
            [
                EvaluatorTestData.Span("root", null, "span-root"),
                EvaluatorTestData.Span("child", null, "span-child", "span-root")
            ]);

        AnalysisResult result = TraceCorrelationEvaluator.Analyze(record);

        Assert.False(result.Passed);
        Assert.Contains("root:traceId", result.Reason, StringComparison.Ordinal);
        Assert.Contains("child:traceId", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_FailsMissingSpanId()
    {
        var record = EvaluatorTestData.Record(
            telemetry: [EvaluatorTestData.Span("broken", "trace-1", null)]);

        AnalysisResult result = TraceCorrelationEvaluator.Analyze(record);

        Assert.False(result.Passed);
        Assert.Contains("broken:spanId", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_FailsDuplicateTraceSpanKey()
    {
        var record = EvaluatorTestData.Record(
            telemetry:
            [
                EvaluatorTestData.Span("first", "trace-1", "span-1"),
                EvaluatorTestData.Span("second", "trace-1", "span-1")
            ]);

        AnalysisResult result = TraceCorrelationEvaluator.Analyze(record);

        Assert.False(result.Passed);
        Assert.Contains("duplicate span keys: trace-1/span-1", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_FailsMissingParent()
    {
        var record = EvaluatorTestData.Record(
            telemetry: [EvaluatorTestData.Span("child", "trace-1", "span-child", "span-missing")]);

        AnalysisResult result = TraceCorrelationEvaluator.Analyze(record);

        Assert.False(result.Passed);
        Assert.Contains("child:span-missing", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsFailedMetricForBrokenTrace()
    {
        var record = EvaluatorTestData.Record(
            telemetry: [EvaluatorTestData.Span("child", "trace-1", "span-child", "span-missing")]);

        EvaluationResult result = await new TraceCorrelationEvaluator(record).EvaluateAsync(
            [],
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "")),
            cancellationToken: TestContext.Current.CancellationToken);

        BooleanMetric metric = result.Get<BooleanMetric>("qyl.trace.correlation");
        Assert.False(metric.Value);
        Assert.True(metric.Interpretation?.Failed);
    }
}
