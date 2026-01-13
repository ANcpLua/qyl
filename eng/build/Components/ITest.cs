using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Serilog;

namespace Components;

[ParameterPrefix(nameof(ITest))]
interface ITest : ICompile
{
    [Parameter("Test filter expression (xUnit v3 query syntax)")]
    string? TestFilter => TryGetValue<string?>(() => TestFilter);

    [Parameter("Stop on first test failure")]
    bool? StopOnFail => TryGetValue<bool?>(() => StopOnFail);

    [Parameter("Show live test output")]
    bool? LiveOutput => TryGetValue<bool?>(() => LiveOutput);

    Project[] TestProjects =>
        Solution.AllProjects
            .Where(p => p.Path?.ToString().Contains("/tests/", StringComparison.Ordinal) == true)
            .ToArray();

    Target SetupTestcontainers => d => d
        .Description("Configure Testcontainers for CI")
        .Unlisted()
        .Executes(() =>
        {
            if (IsServerBuild)
            {
                Environment.SetEnvironmentVariable("DOCKER_HOST", "unix:///var/run/docker.sock");
                Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "false");

                Log.Information("Testcontainers: Configured for CI");
                Log.Debug("  DOCKER_HOST = unix:///var/run/docker.sock");
            }
            else
                Log.Debug("Testcontainers: Using local Docker configuration");
        });

    Target Test => d => d
        .Description("Run all tests")
        .DependsOn(Compile)
        .DependsOn(SetupTestcontainers)
        .Executes(() => RunTests(new TestOptions(TestFilter)));

    Target UnitTests => d => d
        .Description("Run unit tests only")
        .DependsOn(Compile)
        .Executes(() => RunTests(new TestOptions(
            NamespaceFilter: "*.Unit.*",
            ReportPrefix: "Unit")));

    Target IntegrationTests => d => d
        .Description("Run integration tests only")
        .DependsOn(Compile)
        .DependsOn(SetupTestcontainers)
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

        // MTP args (go after --)
        var mtp = MtpExtensions.Mtp()
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

        // Build MTP args for post-"--" section
        var mtpArgs = mtp.BuildProcessArgs();

        // .NET 10 MTP requires explicit --project flag (not positional)
        // --results-directory is a dotnet test arg (before --), MTP args go after --
        var arguments = $"test --project {projectPath} --configuration {Configuration} --no-build --no-restore --results-directory {TestResultsDirectory} {mtpArgs}";

        var process = ProcessTasks.StartProcess(
            toolPath: ToolPathResolver.GetPathExecutable("dotnet"),
            arguments: arguments,
            workingDirectory: RootDirectory,
            logOutput: true);

        process.AssertWaitForExit();

        if (process.ExitCode is not (0 or 8))
            throw new InvalidOperationException($"dotnet test failed with exit code {process.ExitCode}");

        if (process.ExitCode is 8)
            Log.Warning("Zero tests matched filter (exit code 8)");
    }

    public readonly record struct TestOptions(
        string? Filter = null,
        string? NamespaceFilter = null,
        string ReportPrefix = "",
        bool WithCoverage = false,
        AbsolutePath? CoverageOutput = null);
}
