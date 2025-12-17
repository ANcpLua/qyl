using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace Components;

[ParameterPrefix(nameof(ITest))]
interface ITest : ICompile
{
    [Parameter("Test filter expression (xUnit v3 query syntax)")]
    string? TestFilter => null;

    [Parameter("Stop on first test failure")]
    bool? StopOnFail => null;

    [Parameter("Show live test output")] bool? LiveOutput => null;

    Project[] TestProjects =>
        Solution.AllProjects
            .Where(p => p.Path?.ToString().Contains("/tests/", StringComparison.Ordinal) == true)
            .ToArray();

    Target Test => d => d
        .Description("Run all tests")
        .DependsOn<ICompile>(x => x.Compile)
        .TryDependsOn<ITestContainers>()
        .Executes(() => RunTests(new TestOptions(TestFilter)));

    Target UnitTests => d => d
        .Description("Run unit tests only")
        .DependsOn<ICompile>(x => x.Compile)
        .Executes(() => RunTests(new TestOptions(
            NamespaceFilter: "*.Unit.*",
            ReportPrefix: "Unit")));

    Target IntegrationTests => d => d
        .Description("Run integration tests only")
        .DependsOn<ICompile>(x => x.Compile)
        .TryDependsOn<ITestContainers>()
        .Executes(() => RunTests(new TestOptions(
            NamespaceFilter: "*.Integration.*",
            ReportPrefix: "Integration")));

    private void RunTests(TestOptions options)
    {
        foreach (var project in TestProjects) RunTestProject(project, options);
    }

    void RunTestProject(Project project, TestOptions options)
    {
        var reportName = options.ReportPrefix is { Length: > 0 } prefix
            ? $"{project.Name}.{prefix}.trx"
            : $"{project.Name}.trx";

        Log.Information("Running tests: {Project}", project.Name);

        var mtp = MtpExtensions.Mtp()
            .ResultsDirectory(TestResultsDirectory)
            .ReportTrx(reportName)
            .IgnoreExitCode(8);

        if (options.Filter is { Length: > 0 } filter)
            mtp.FilterQuery(filter);

        if (options.NamespaceFilter is { Length: > 0 } ns)
            mtp.FilterNamespace(ns);

        if (options is { WithCoverage: true, CoverageOutput: { } coverageFile })
            mtp.CoverageCobertura(coverageFile);

        if (StopOnFail == true)
            mtp.StopOnFail();

        if (LiveOutput == true || IsLocalBuild)
            mtp.ShowLiveOutput();

        ExecuteMtpTestInternal(project, mtp);
    }

    void ExecuteMtpTestInternal(Project project, MtpArgumentsBuilder mtp)
    {
        var projectPath = project.Path ?? throw new InvalidOperationException($"Project '{project.Name}' has no path");
        var settings = new DotNetTestSettings()
            .SetProjectFile(projectPath)
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .EnableNoRestore()
            .SetProcessWorkingDirectory(RootDirectory)
            .SetProcessExitHandler(process =>
            {
                if (process.ExitCode is not (0 or 8))
                    process.AssertZeroExitCode();

                if (process.ExitCode is 8)
                    Log.Warning("Zero tests matched filter (exit code 8)");
            });

        var mtpArgs = mtp.BuildArgs();
        if (mtpArgs.Count > 0)
            settings = settings.AddProcessAdditionalArguments(["--", .. mtpArgs]);

        DotNetTest(settings);
    }

    public readonly record struct TestOptions(
        string? Filter = null,
        string? NamespaceFilter = null,
        string ReportPrefix = "",
        bool WithCoverage = false,
        AbsolutePath? CoverageOutput = null);
}