using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Qyl.Observability.Evaluation.Evaluators;

namespace Qyl.Observability.Evaluation.Tests;

public sealed class TelemetryEvidenceEvaluatorTests
{
    [Fact]
    public void Analyze_PassesWhenEvidenceExistsIsCitedAndForbiddenClaimsAreAbsent()
    {
        var record = EvaluatorTestData.Record(
            finalResponse: "Evidence ev-metric-latency explains the spike.",
            telemetry: [EvaluatorTestData.Metric("ev-metric-latency")],
            requiredEvidenceIds: ["ev-metric-latency"],
            forbiddenClaims: ["database saturation"]);

        AnalysisResult result = TelemetryEvidenceEvaluator.Analyze(record);

        Assert.True(result.Passed, result.Reason);
    }

    [Fact]
    public void Analyze_FailsMissingTelemetryMissingCitationAndForbiddenClaim()
    {
        var record = EvaluatorTestData.Record(
            finalResponse: "The issue is database saturation.",
            telemetry: [EvaluatorTestData.Metric("ev-present")],
            requiredEvidenceIds: ["ev-missing", "ev-present"],
            forbiddenClaims: ["database saturation"]);

        AnalysisResult result = TelemetryEvidenceEvaluator.Analyze(record);

        Assert.False(result.Passed);
        Assert.Contains("missing telemetry: ev-missing", result.Reason, StringComparison.Ordinal);
        Assert.Contains("missing citations: ev-missing, ev-present", result.Reason, StringComparison.Ordinal);
        Assert.Contains("forbidden claims: database saturation", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsPassedMetricForGroundedAnswer()
    {
        var record = EvaluatorTestData.Record(
            finalResponse: "Evidence ev-log-error supports the conclusion.",
            telemetry: [EvaluatorTestData.Metric("ev-log-error")],
            requiredEvidenceIds: ["ev-log-error"]);

        EvaluationResult result = await new TelemetryEvidenceEvaluator(record).EvaluateAsync(
            [],
            new ChatResponse(new ChatMessage(ChatRole.Assistant, record.FinalResponse)),
            cancellationToken: TestContext.Current.CancellationToken);

        BooleanMetric metric = result.Get<BooleanMetric>("qyl.telemetry.evidence");
        Assert.True(metric.Value);
        Assert.False(metric.Interpretation?.Failed);
    }
}
