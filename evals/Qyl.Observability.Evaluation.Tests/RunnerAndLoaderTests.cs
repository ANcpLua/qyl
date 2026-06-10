using Microsoft.Extensions.AI.Evaluation;
using Qyl.Observability.Evaluation;
using Qyl.Observability.Evaluation.Evaluators;

namespace Qyl.Observability.Evaluation.Tests;

public sealed class RunnerAndLoaderTests
{
    [Fact]
    public void ScenarioRunResult_FailsWhenPassRecordDeclaresExpectedFailures()
    {
        var record = EvaluatorTestData.Record(expectedFailedMetrics: ["qyl.trace.correlation"], shouldPass: true);

        ScenarioRunResult result = ScenarioRunResult.Create(record, [PassedMetric("qyl.trace.correlation")]);

        Assert.False(result.Passed);
        Assert.Contains("pass record declares expected failed metrics", result.Mismatches);
    }

    [Fact]
    public void ScenarioRunResult_FailsWhenFailRecordDeclaresNoExpectedFailures()
    {
        var record = EvaluatorTestData.Record(shouldPass: false);

        ScenarioRunResult result = ScenarioRunResult.Create(record, [FailedMetric("qyl.trace.correlation")]);

        Assert.False(result.Passed);
        Assert.Contains("fail record declares no expected failed metrics", result.Mismatches);
    }

    [Fact]
    public void ScenarioRunResult_FailsWhenActualFailedMetricsDifferFromExpected()
    {
        var record = EvaluatorTestData.Record(expectedFailedMetrics: ["qyl.tool.call.accuracy"], shouldPass: false);

        ScenarioRunResult result = ScenarioRunResult.Create(record, [FailedMetric("qyl.trace.correlation")]);

        Assert.False(result.Passed);
        Assert.Contains("failed metrics [qyl.trace.correlation] != expected [qyl.tool.call.accuracy]", result.Mismatches);
    }

    [Fact]
    public async Task EvaluationRunner_MatchesExpectedFailedMetrics()
    {
        var records = new[]
        {
            EvaluatorTestData.Record(
                finalResponse: "Evidence ev-metric supports this.",
                telemetry: [EvaluatorTestData.Metric("ev-metric")],
                requiredEvidenceIds: ["ev-metric"],
                shouldPass: true),
            EvaluatorTestData.Record(
                finalResponse: "No citation.",
                requiredEvidenceIds: ["ev-missing"],
                expectedFailedMetrics: ["qyl.telemetry.evidence"],
                shouldPass: false)
        };

        IReadOnlyList<ScenarioRunResult> results = await EvaluationRunner.RunAsync(records, TestContext.Current.CancellationToken);

        Assert.All(results, static result => Assert.True(result.Passed, string.Join("; ", result.Mismatches)));
    }

    [Fact]
    public void ScenarioLoader_LoadsJsonlAndSkipsBlankLines()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jsonl");
        File.WriteAllText(path, """
{"id":"fixture","source":"tests","scenario":"load","agent":{"name":"agent","modelProvider":"fixture","modelName":"model","instructions":"instructions","tools":[]},"userInput":"input","toolCalls":[],"finalResponse":"response","telemetry":[],"requiredEvidenceIds":[],"forbiddenClaims":[],"expectedToolCalls":[],"expectedFailedMetrics":[],"shouldPass":true}

""");

        try
        {
            var records = ScenarioLoader.LoadJsonl(path);

            Assert.Single(records);
            Assert.Equal("fixture", records[0].Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static BooleanMetric PassedMetric(string name)
        => EvaluationMetricFactory.CreateBoolean(name, true, "passed");

    private static BooleanMetric FailedMetric(string name)
        => EvaluationMetricFactory.CreateBoolean(name, false, "failed");
}
