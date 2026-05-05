#nullable enable

namespace Qyl.Domains.Observe.Test;

public sealed class TestAttributes
{
    public required string CaseName { get; init; }
    public Qyl.Domains.Observe.Test.TestResultStatus? CaseResultStatus { get; init; }
    public string? SuiteName { get; init; }
    public Qyl.Domains.Observe.Test.TestSuiteRunStatus? SuiteRunStatus { get; init; }
}

public sealed class TestRunEntity
{
    public required string RunId { get; init; }
    public required string SuiteName { get; init; }
    public required Qyl.Domains.Observe.Test.TestSuiteRunStatus Status { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public long? DurationMs { get; init; }
    public required int TotalCount { get; init; }
    public required int PassedCount { get; init; }
    public required int FailedCount { get; init; }
    public required int SkippedCount { get; init; }
    public int? FlakyCount { get; init; }
    public Qyl.Domains.Observe.Test.TestFramework? Framework { get; init; }
    public string? CicdRunId { get; init; }
    public string? GitCommit { get; init; }
    public string? GitBranch { get; init; }
}

public sealed class TestCaseEntity
{
    public required string CaseId { get; init; }
    public required string RunId { get; init; }
    public required string Name { get; init; }
    public string? FilePath { get; init; }
    public required Qyl.Domains.Observe.Test.TestResultStatus Status { get; init; }
    public long? DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
    public int? RetryCount { get; init; }
    public bool? IsFlaky { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
}

public sealed class TestRunCountMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string SuiteName { get; init; }
    public required Qyl.Domains.Observe.Test.TestSuiteRunStatus Status { get; init; }
}

public sealed class TestCaseCountMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string SuiteName { get; init; }
    public required Qyl.Domains.Observe.Test.TestResultStatus ResultStatus { get; init; }
}

public sealed class TestDurationMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string SuiteName { get; init; }
}

public sealed class CoverageReport
{
    public required string RunId { get; init; }
    public required double LineCoverage { get; init; }
    public double? BranchCoverage { get; init; }
    public double? FunctionCoverage { get; init; }
    public double? StatementCoverage { get; init; }
    public IReadOnlyList<Qyl.Domains.Observe.Test.FileCoverage>? ByFile { get; init; }
}

public sealed class FileCoverage
{
    public required string FilePath { get; init; }
    public required double LineCoverage { get; init; }
    public required int CoveredLines { get; init; }
    public required int TotalLines { get; init; }
    public IReadOnlyList<int>? UncoveredLines { get; init; }
}

public enum TestResultStatus
{
    Pass,
    Fail
}

public enum TestSuiteRunStatus
{
    Success,
    Failure,
    Skipped,
    Aborted,
    TimedOut,
    InProgress
}

public enum TestFramework
{
    Jest,
    Mocha,
    Vitest,
    Playwright,
    Cypress,
    Selenium,
    Pytest,
    Unittest,
    Junit,
    Testng,
    Xunit,
    Nunit,
    Mstest,
    Rspec,
    GoTest,
    CargoTest,
    Other
}
