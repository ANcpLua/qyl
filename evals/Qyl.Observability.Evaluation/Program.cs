using Qyl.Observability.Evaluation;

string root = AppContext.BaseDirectory;
string[] scenarioPaths = args.Length > 0
    ? args
    : [Path.Combine(root, "Data", "incident-triage.jsonl")];

var records = new List<Qyl.Observability.Evaluation.Models.ObservabilityEvaluationRecord>();
foreach (string scenarioPath in scenarioPaths)
{
    string fullPath = Path.GetFullPath(scenarioPath);
    records.AddRange(ScenarioLoader.LoadJsonl(fullPath));
}

IReadOnlyList<ScenarioRunResult> results = await EvaluationRunner.RunAsync(records, CancellationToken.None);

foreach (ScenarioRunResult result in results)
{
    string status = result.Passed ? "PASS" : "FAIL";
    Console.WriteLine($"{result.Id}: {status}");

    foreach (string mismatch in result.Mismatches)
    {
        Console.Error.WriteLine($"  {mismatch}");
    }
}

int failed = results.Count(static result => !result.Passed);
Console.WriteLine($"qyl observability eval: {results.Count - failed}/{results.Count} scenarios passed");

return failed == 0 ? 0 : 1;
