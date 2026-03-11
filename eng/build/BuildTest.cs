// =============================================================================
// qyl Build System - Test Execution
// =============================================================================
// MTP argument builder + IQylTest (ITest + xUnit v3 + MTP)
// =============================================================================


// ════════════════════════════════════════════════════════════════════════════════
// MTP (Microsoft Testing Platform) Extensions
// ════════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Components;
using Serilog;

public static class MtpExtensions
{
    public static MtpArgumentsBuilder Mtp() => new();
}

public sealed class MtpArgumentsBuilder
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

    // ─── Filters ────────────────────────────────────────────────────────────
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

    // ─── Reports ────────────────────────────────────────────────────────────
    public MtpArgumentsBuilder ReportTrx(string filename)
    {
        _args.Add("--report-trx");
        return AddOption("--report-trx-filename", filename);
    }

    // ─── Execution Options ──────────────────────────────────────────────────
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

    // ─── Coverage ───────────────────────────────────────────────────────────
    public MtpArgumentsBuilder CoverageCobertura(AbsolutePath outputPath)
    {
        _args.Add("--coverage");
        _args.Add("--coverage-output-format");
        _args.Add("cobertura");
        return AddOption("--coverage-output", outputPath.ToString());
    }

    // ─── Output ─────────────────────────────────────────────────────────────
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

    /// <summary>
    ///     Builds a properly-escaped argument string for passthrough after --.
    ///     Returns empty string if no args, otherwise "-- arg1 arg2 ...".
    /// </summary>
    public string BuildProcessArgs()
    {
        if (_args.Count is 0) return string.Empty;
        return "-- " + string.Join(" ", _args.Select(StringExtensions.DoubleQuoteIfNeeded));
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// IQylTest - Test Execution via ITest + MTP
// ════════════════════════════════════════════════════════════════════════════════

[ParameterPrefix(nameof(IQylTest))]
interface IQylTest : ITest, IHazSourcePaths
{
    [Parameter("Test filter expression (xUnit v3 query syntax)")]
    string? TestFilter => TryGetValue(() => TestFilter);

    [Parameter("Stop on first test failure")]
    bool? StopOnFail => TryGetValue<bool?>(() => StopOnFail);

    [Parameter("Show live test output")] bool? LiveOutput => TryGetValue<bool?>(() => LiveOutput);

    Target UnitTests => d => d
        .Description("Run unit tests only")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => RunFilteredTests("*.Unit.*", "Unit", needsTestcontainers: false));

    Target IntegrationTests => d => d
        .Description("Run integration tests only")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => RunFilteredTests("*.Integration.*", "Integration", needsTestcontainers: true));

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

    // Override test project discovery (tests/ dir, not *.Tests naming)
    IEnumerable<Project> ITest.TestProjects =>
        Solution.AllProjects.Where(static p =>
            p.Path?.ToString().Contains("/tests/", StringComparison.Ordinal) == true);

    // Override results directory
    Configure<DotNetTestSettings> ITest.TestSettings => s =>
    {
        EnsureTestcontainersConfigured();
        return s.SetResultsDirectory(TestResultsDirectory);
    };

    // MTP arguments per project (replaces ExecuteMtpTestInternal)
    // .NET 10 SDK: dotnet test requires --project flag (positional arg removed)
    Configure<DotNetTestSettings, Project> ITest.TestProjectSettings => (s, project) =>
    {
        var mtp = MtpExtensions.Mtp()
            .ReportTrx($"{project.Name}.trx")
            .IgnoreExitCode(8);

        if (TestFilter is { Length: > 0 } f) mtp.FilterQuery(f);
        if (StopOnFail == true) mtp.StopOnFail();
        if (LiveOutput == true || IsLocalBuild) mtp.ShowLiveOutput();

        // NUKE 10.1.0 uses positional arg for project, but .NET 10 requires --project flag.
        // Clear --logger (conflicts with MTP's --report-trx), reset positional arg,
        // pass --project + MTP args via additional arguments.
        var projectPath = project.Path ?? throw new InvalidOperationException($"Project '{project.Name}' has no path");
        string[] additionalArgs = ["--project", projectPath.ToString(), .. mtp.BuildArgs().Prepend("--")];
        return s.ClearLoggers().ResetProjectFile().SetProcessAdditionalArguments(additionalArgs);
    };

    Target TestSummary => d => d
        .Description("Generate Markdown test summary from MTP TRX reports")
        .After<IQylTest>(static x => x.Test)
        .Executes(WriteGitHubTestSummary);

    sealed void EnsureTestcontainersConfigured()
    {
        if (IsServerBuild)
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "unix:///var/run/docker.sock");
            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "false");
            Log.Information("Testcontainers: Configured for CI");
        }
    }

    /// <summary>
    ///     Parses MTP-produced TRX files and writes a Markdown summary.
    ///     Writes to <c>$GITHUB_STEP_SUMMARY</c> when running in GitHub Actions,
    ///     and always writes <c>Artifacts/test-summary.md</c>.
    /// </summary>
    sealed void WriteGitHubTestSummary()
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
            var doc = XDocument.Load(trxFile);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
            if (counters is null) continue;

            var passed = int.Parse(counters.Attribute("passed")?.Value ?? "0", CultureInfo.InvariantCulture);
            var failed = int.Parse(counters.Attribute("failed")?.Value ?? "0", CultureInfo.InvariantCulture);
            var skipped = int.Parse(
                counters.Attribute("notExecuted")?.Value ?? counters.Attribute("inconclusive")?.Value ?? "0",
                CultureInfo.InvariantCulture);

            // Sum durations from individual test results (more accurate than wall-clock)
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

        // Always write to artifacts
        var artifactPath = ((IHazSourcePaths)this).ArtifactsDirectory / "test-summary.md";
        artifactPath.Parent.CreateDirectory();
        File.WriteAllText(artifactPath, markdown);
        Log.Information("Test summary: {Path}", artifactPath);

        // Write to GitHub step summary when available
        var stepSummaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (stepSummaryPath is { Length: > 0 })
        {
            File.AppendAllText(stepSummaryPath, markdown);
            Log.Information("Test summary written to $GITHUB_STEP_SUMMARY");
        }
    }

    private static string FormatDuration(TimeSpan duration) => duration.TotalMinutes >= 1
        ? $"{duration.TotalMinutes:F1}m"
        : $"{duration.TotalSeconds:F1}s";
}