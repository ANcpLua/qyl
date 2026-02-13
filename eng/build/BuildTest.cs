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
using System.Linq;
using System.Text;
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

    public MtpArgumentsBuilder ReportXunit(string filename)
    {
        _args.Add("--report-xunit");
        return AddOption("--report-xunit-filename", filename);
    }

    public MtpArgumentsBuilder ReportJunit(string filename)
    {
        _args.Add("--report-junit");
        return AddOption("--report-junit-filename", filename);
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

    public IReadOnlyList<string> BuildArgs() => _args;

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

    [Parameter("Show live test output")]
    bool? LiveOutput => TryGetValue<bool?>(() => LiveOutput);

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

    sealed void EnsureTestcontainersConfigured()
    {
        if (IsServerBuild)
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "unix:///var/run/docker.sock");
            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "false");
            Log.Information("Testcontainers: Configured for CI");
        }
    }

    Target UnitTests => d => d
        .Description("Run unit tests only")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            DotNetTasks.DotNetTest(s => s
                .SetNoBuild(true)
                .SetNoRestore(true)
                .SetResultsDirectory(TestResultsDirectory)
                .CombineWith(TestProjects, (ss, project) =>
                {
                    var mtp = MtpExtensions.Mtp()
                        .ReportTrx($"{project.Name}.Unit.trx")
                        .IgnoreExitCode(8)
                        .FilterNamespace("*.Unit.*");

                    if (StopOnFail == true) mtp.StopOnFail();
                    if (LiveOutput == true || IsLocalBuild) mtp.ShowLiveOutput();

                    var projectPath = project.Path ?? throw new InvalidOperationException($"Project '{project.Name}' has no path");
                    string[] unitArgs = ["--project", projectPath.ToString(), .. mtp.BuildArgs().Prepend("--")];
                    return ss.SetProcessAdditionalArguments(unitArgs);
                }), completeOnFailure: true);
        });

    Target IntegrationTests => d => d
        .Description("Run integration tests only")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            EnsureTestcontainersConfigured();
            DotNetTasks.DotNetTest(s => s
                .SetNoBuild(true)
                .SetNoRestore(true)
                .SetResultsDirectory(TestResultsDirectory)
                .CombineWith(TestProjects, (ss, project) =>
                {
                    var mtp = MtpExtensions.Mtp()
                        .ReportTrx($"{project.Name}.Integration.trx")
                        .IgnoreExitCode(8)
                        .FilterNamespace("*.Integration.*");

                    if (StopOnFail == true) mtp.StopOnFail();
                    if (LiveOutput == true || IsLocalBuild) mtp.ShowLiveOutput();

                    var projectPath = project.Path ?? throw new InvalidOperationException($"Project '{project.Name}' has no path");
                    string[] integrationArgs = ["--project", projectPath.ToString(), .. mtp.BuildArgs().Prepend("--")];
                    return ss.SetProcessAdditionalArguments(integrationArgs);
                }), completeOnFailure: true);
        });
}
