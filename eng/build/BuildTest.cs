


using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Components;
using Serilog;

namespace Qyl.Build;

static class MtpExtensions
{
    public static MtpArgumentsBuilder Mtp() => new();
}

sealed class MtpArgumentsBuilder
{
    readonly List<string> _args = [];

    MtpArgumentsBuilder AddFilter(string option, params ReadOnlySpan<string> patterns)
    {
        foreach (var p in patterns)
        {
            if (string.IsNullOrEmpty(p)) continue;
            _args.Add(option);
            _args.Add(p);
        }

        return this;
    }

    MtpArgumentsBuilder AddOption(string option, string value)
    {
        _args.Add(option);
        _args.Add(value);
        return this;
    }

    MtpArgumentsBuilder AddFlag(string option)
    {
        _args.Add(option);
        _args.Add("on");
        return this;
    }

    public MtpArgumentsBuilder FilterNamespace(params ReadOnlySpan<string> patterns) =>
        AddFilter("--filter-namespace", patterns);

    public MtpArgumentsBuilder FilterNotNamespace(params ReadOnlySpan<string> patterns) =>
        AddFilter("--filter-not-namespace", patterns);

    public MtpArgumentsBuilder FilterClass(params ReadOnlySpan<string> patterns) =>
        AddFilter("--filter-class", patterns);

    public MtpArgumentsBuilder FilterNotClass(params ReadOnlySpan<string> patterns) =>
        AddFilter("--filter-not-class", patterns);

    public MtpArgumentsBuilder FilterMethod(params ReadOnlySpan<string> patterns) =>
        AddFilter("--filter-method", patterns);

    public MtpArgumentsBuilder FilterTrait(string name, string value) =>
        AddOption("--filter-trait", $"{name}={value}");

    public MtpArgumentsBuilder FilterNotTrait(string name, string value) =>
        AddOption("--filter-not-trait", $"{name}={value}");

    public MtpArgumentsBuilder FilterQuery(string? expression) =>
        string.IsNullOrEmpty(expression) ? this : AddOption("--filter-query", expression);

    public MtpArgumentsBuilder ReportTrx(string filename)
    {
        _args.Add("--report-trx");
        return AddOption("--report-trx-filename", filename);
    }

    public MtpArgumentsBuilder StopOnFail() => AddFlag("--stop-on-fail");

    public MtpArgumentsBuilder MaxThreads(int count) =>
        AddOption("--max-threads", count.ToString(CultureInfo.InvariantCulture));

    public MtpArgumentsBuilder Timeout(TimeSpan duration) =>
        AddOption("--timeout", $"{(int)duration.TotalSeconds}s");

    public MtpArgumentsBuilder IgnoreExitCode(int code) =>
        AddOption("--ignore-exit-code", code.ToString(CultureInfo.InvariantCulture));

    public MtpArgumentsBuilder MinimumExpectedTests(int count) =>
        AddOption("--minimum-expected-tests", count.ToString(CultureInfo.InvariantCulture));

    public MtpArgumentsBuilder Seed(int seed) =>
        AddOption("--seed", seed.ToString(CultureInfo.InvariantCulture));

    public MtpArgumentsBuilder ShowLiveOutput() => AddFlag("--show-live-output");

    public MtpArgumentsBuilder Diagnostics() => AddFlag("--xunit-diagnostics");

    public MtpArgumentsBuilder CoverageCobertura(AbsolutePath outputPath)
    {
        _args.Add("--coverage");
        _args.Add("--coverage-output-format");
        _args.Add("cobertura");
        return AddOption("--coverage-output", outputPath.ToString());
    }

    public string Build()
    {
        if (_args is []) return string.Empty;

        StringBuilder sb = new();
        foreach (var arg in _args)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(arg.DoubleQuoteIfNeeded());
        }

        return sb.ToString();
    }

    public IEnumerable<string> BuildArgs() => _args;

    public string BuildProcessArgs()
    {
        if (_args.Count is 0) return string.Empty;
        return "-- " + string.Join(" ", _args.Select(StringExtensions.DoubleQuoteIfNeeded));
    }
}


[ParameterPrefix(nameof(IQylTest))]
interface IQylTest : ITest, IHazSourcePaths
{
    [Parameter("Test filter expression (xUnit v3 query syntax)")]
    string? TestFilter => TryGetValue(() => TestFilter);

    [Parameter("Stop on first test failure")]
    bool? StopOnFail => TryGetValue<bool?>(() => StopOnFail);

    [Parameter("Show live test output")] bool? LiveOutput => TryGetValue<bool?>(() => LiveOutput);

    Target UnitTests => d => d
        .Unlisted()
        .Description("Run unit tests only")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => RunFilteredTests("*.Unit.*", "Unit", false));

    Target IntegrationTests => d => d
        .Unlisted()
        .Description("Run integration tests only")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => RunFilteredTests("*.Integration.*", "Integration", true));

    Target FunctionalTests => d => d
        .Unlisted()
        .Description("Run functional tests only (in-process hosting, external boundaries stubbed)")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            RunFilteredTests("*.Functional.*", "Functional", false);
            // Guard against silent "namespace filter matched zero tests" runs.
            // RunFilteredTests passes IgnoreExitCode(8), which swallows MTP's
            // "no tests discovered" exit. Post-run TRX inspection is the
            // load-bearing check: at least one functional test MUST have
            // executed somewhere in the suite for this target to succeed.
            AssertAtLeastOneTestExecuted("Functional");
        });

    Target E2ETests => d => d
        .Description("Run end-to-end tests (full Docker topology against freshly built images)")
        .DependsOn<ICompile>(static x => x.Compile)
        .DependsOn<IDocker>(static x => x.DockerImageBuild)
        .Executes(() => RunFilteredE2ETests());

    Target TestSummary => d => d
        .Unlisted()
        .Description("Generate Markdown test summary from MTP TRX reports")
        .After<IQylTest>(static x => x.Test)
        .Executes(WriteGitHubTestSummaryAsync);

    IEnumerable<Project> ITest.TestProjects =>
        Solution.AllProjects.Where(static p =>
            p.Path?.ToString().Contains("/tests/", StringComparison.Ordinal) == true);

    Configure<DotNetTestSettings> ITest.TestSettings => s =>
    {
        EnsureTestcontainersConfigured();
        return s.SetResultsDirectory(TestResultsDirectory);
    };

    Configure<DotNetTestSettings, Project> ITest.TestProjectSettings => (s, project) =>
    {
        var mtp = MtpExtensions.Mtp()
            .ReportTrx($"{project.Name}.trx")
            .IgnoreExitCode(8);

        // Heavy/opt-in tests (Category=regen — shell out to Weaver; Category=E2E — full
        // Docker topology via DockerImageBuild) are excluded from the default Test run;
        // pass --IQylTest.TestFilter to include them, or run the dedicated sub-target
        // (E2ETests). E2EBootstrap-traited tests (no Docker) intentionally stay in.
        if (TestFilter is { Length: > 0 } f) mtp.FilterQuery(f);
        else mtp.FilterNotTrait("Category", "regen").FilterNotTrait("Category", "E2E");

        if (StopOnFail == true) mtp.StopOnFail();
        if (LiveOutput == true || IsLocalBuild) mtp.ShowLiveOutput();

        var projectPath = project.Path ?? throw new InvalidOperationException($"Project '{project.Name}' has no path");
        string[] additionalArgs = ["--project", projectPath.ToString(), .. mtp.BuildArgs().Prepend("--")];
        return s.ClearLoggers().ResetProjectFile().SetProcessAdditionalArguments(additionalArgs);
    };

    sealed void RunFilteredTests(string namespaceFilter, string trxSuffix, bool needsTestcontainers)
    {
        if (needsTestcontainers) EnsureTestcontainersConfigured();

        DotNetTasks.DotNetTest(s => s
            .SetNoBuild(true)
            .SetNoRestore(true)
            .SetResultsDirectory(TestResultsDirectory)
            .CombineWith(TestProjects, (ss, project) =>
            {
                var mtp = MtpExtensions.Mtp()
                    .ReportTrx($"{project.Name}.{trxSuffix}.trx")
                    .IgnoreExitCode(8)
                    .FilterNamespace(namespaceFilter);

                if (StopOnFail == true) mtp.StopOnFail();
                if (LiveOutput == true || IsLocalBuild) mtp.ShowLiveOutput();

                var projectPath = project.Path ??
                                  throw new InvalidOperationException($"Project '{project.Name}' has no path");
                string[] args = ["--project", projectPath.ToString(), .. mtp.BuildArgs().Prepend("--")];
                return ss.SetProcessAdditionalArguments(args);
            }), completeOnFailure: true);
    }

    sealed void RunFilteredE2ETests()
    {
        EnsureTestcontainersConfigured();

        var e2eProjects = TestProjects
            .Where(static p => p.Name.Equals("qyl.e2e.tests", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (e2eProjects.Length is 0)
        {
            Log.Warning("E2ETests: no qyl.e2e.tests project found; skipping");
            return;
        }

        DotNetTasks.DotNetTest(s => s
            .SetNoBuild(true)
            .SetNoRestore(true)
            .SetResultsDirectory(TestResultsDirectory)
            .CombineWith(e2eProjects, (ss, project) =>
            {
                var mtp = MtpExtensions.Mtp()
                    .ReportTrx($"{project.Name}.E2E.trx")
                    .IgnoreExitCode(8)
                    .FilterTrait("Category", "E2E");

                if (StopOnFail == true) mtp.StopOnFail();
                if (LiveOutput == true || IsLocalBuild) mtp.ShowLiveOutput();

                var projectPath = project.Path ??
                                  throw new InvalidOperationException($"Project '{project.Name}' has no path");
                string[] args = ["--project", projectPath.ToString(), .. mtp.BuildArgs().Prepend("--")];
                return ss.SetProcessAdditionalArguments(args);
            }), completeOnFailure: true);
    }

    sealed void EnsureTestcontainersConfigured()
    {
        if (IsServerBuild)
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "unix:///var/run/docker.sock");
            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "false");
            Log.Information("Testcontainers: Configured for CI");
        }
    }

    sealed void AssertAtLeastOneTestExecuted(string trxSuffix)
    {
        var trxFiles = TestResultsDirectory.GlobFiles($"**/*.{trxSuffix}.trx");
        if (trxFiles.Count is 0)
            throw new InvalidOperationException(
                $"{trxSuffix}Tests target produced no TRX reports under {TestResultsDirectory}. " +
                "Namespace filter likely matched zero tests across every test project. " +
                "Add tests in the filtered namespace, or fix the namespace filter.");

        var totalExecuted = 0;
        foreach (var trxFile in trxFiles)
        {
            var doc = XDocument.Parse(File.ReadAllText(trxFile));
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
            if (counters is null) continue;

            var passed = int.Parse(counters.Attribute("passed")?.Value ?? "0", CultureInfo.InvariantCulture);
            var failed = int.Parse(counters.Attribute("failed")?.Value ?? "0", CultureInfo.InvariantCulture);
            var skipped = int.Parse(
                counters.Attribute("notExecuted")?.Value ?? counters.Attribute("inconclusive")?.Value ?? "0",
                CultureInfo.InvariantCulture);
            totalExecuted += passed + failed + skipped;
        }

        if (totalExecuted < 1)
            throw new InvalidOperationException(
                $"{trxSuffix}Tests target ran but zero tests were executed across all projects. " +
                "Namespace filter likely matched nothing (drift, rename, or stale binaries).");

        Log.Information("{Suffix}Tests: {Count} test result(s) recorded across {Files} TRX file(s)",
            trxSuffix, totalExecuted, trxFiles.Count);
    }

    sealed async Task WriteGitHubTestSummaryAsync()
    {
        var trxFiles = TestResultsDirectory.GlobFiles("**/*.trx");
        if (trxFiles.Count is 0)
        {
            Log.Debug("No TRX files found for summary generation");
            return;
        }

        Log.Information("Generating test summary from {Count} MTP TRX report(s)", trxFiles.Count);

        var sb = new StringBuilder();
        sb.AppendLine("## Test Results");
        sb.AppendLine();
        sb.AppendLine("| Project | Passed | Failed | Skipped | Duration |");
        sb.AppendLine("|---------|-------:|-------:|--------:|---------:|");

        var totalPassed = 0;
        var totalFailed = 0;
        var totalSkipped = 0;
        var totalDuration = TimeSpan.Zero;
        var failures = new List<(string Project, string Test, string? Message, string? StackTrace)>();

        foreach (var trxFile in trxFiles.OrderBy(static f => f.Name))
        {
            var doc = XDocument.Parse(await File.ReadAllTextAsync(trxFile));
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
            if (counters is null) continue;

            var passed = int.Parse(counters.Attribute("passed")?.Value ?? "0", CultureInfo.InvariantCulture);
            var failed = int.Parse(counters.Attribute("failed")?.Value ?? "0", CultureInfo.InvariantCulture);
            var skipped = int.Parse(
                counters.Attribute("notExecuted")?.Value ?? counters.Attribute("inconclusive")?.Value ?? "0",
                CultureInfo.InvariantCulture);

            var projectDuration = TimeSpan.Zero;
            foreach (var result in doc.Descendants(ns + "UnitTestResult"))
            {
                if (TimeSpan.TryParse(result.Attribute("duration")?.Value, CultureInfo.InvariantCulture, out var d))
                    projectDuration += d;

                var outcome = result.Attribute("outcome")?.Value;
                if (string.Equals(outcome, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    var testName = result.Attribute("testName")?.Value ?? "Unknown";
                    var errorInfo = result.Descendants(ns + "ErrorInfo").FirstOrDefault();
                    var message = errorInfo?.Element(ns + "Message")?.Value;
                    var stackTrace = errorInfo?.Element(ns + "StackTrace")?.Value;
                    failures.Add((Path.GetFileNameWithoutExtension(trxFile), testName, message, stackTrace));
                }
            }

            var projectName = Path.GetFileNameWithoutExtension(trxFile);
            var failedStr = failed > 0 ? $"**{failed}**" : "0";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {projectName} | {passed} | {failedStr} | {skipped} | {FormatDuration(projectDuration)} |");

            totalPassed += passed;
            totalFailed += failed;
            totalSkipped += skipped;
            totalDuration += projectDuration;
        }

        var totalFailedStr = totalFailed > 0 ? $"**{totalFailed}**" : "0";
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| **Total** | **{totalPassed}** | {totalFailedStr} | **{totalSkipped}** | **{FormatDuration(totalDuration)}** |");

        if (failures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Failures");
            sb.AppendLine();
            foreach (var (project, test, message, stackTrace) in failures)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"<details>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<summary><b>{project}</b>: {test}</summary>");
                sb.AppendLine();
                if (message is { Length: > 0 })
                {
                    sb.AppendLine("```");
                    sb.AppendLine(message.Length > 500 ? string.Concat(message.AsSpan(0, 500), "...") : message);
                    sb.AppendLine("```");
                }

                if (stackTrace is { Length: > 0 })
                {
                    sb.AppendLine("```");
                    sb.AppendLine(stackTrace.Length > 1000
                        ? string.Concat(stackTrace.AsSpan(0, 1000), "...")
                        : stackTrace);
                    sb.AppendLine("```");
                }

                sb.AppendLine("</details>");
                sb.AppendLine();
            }
        }

        var markdown = sb.ToString();

        var artifactPath = ArtifactsDirectory / "test-summary.md";
        artifactPath.Parent.CreateDirectory();
        await File.WriteAllTextAsync(artifactPath, markdown);
        Log.Information("Test summary: {Path}", artifactPath);

        var stepSummaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (stepSummaryPath is { Length: > 0 })
        {
            await File.AppendAllTextAsync(stepSummaryPath, markdown);
            Log.Information("Test summary written to $GITHUB_STEP_SUMMARY");
        }
    }

    private static string FormatDuration(TimeSpan duration) => duration.TotalMinutes >= 1
        ? $"{duration.TotalMinutes:F1}m"
        : $"{duration.TotalSeconds:F1}s";
}
