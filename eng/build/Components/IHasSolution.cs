using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;

namespace Components;

internal interface IHasSolution : INukeBuild
{
    [Solution(GenerateProjects = true)]
    Solution Solution => TryGetValue(() => Solution)!;

    AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";

    AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";

    AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";

    AbsolutePath ComposeFile => SourceDirectory / "compose.yaml";

    AbsolutePath EnvFile => RootDirectory / ".env";

    AbsolutePath SourceDirectory => RootDirectory / "src";

    AbsolutePath CollectorDirectory => SourceDirectory / "qyl.collector";

    AbsolutePath DashboardDirectory => SourceDirectory / "qyl.dashboard";

    AbsolutePath GrpcDirectory => SourceDirectory / "qyl.grpc";

    AbsolutePath McpServerDirectory => SourceDirectory / "qyl.mcp.server";

    AbsolutePath AgentsTelemetryDirectory => SourceDirectory / "qyl.agents.telemetry";

    AbsolutePath SdkAspNetCoreDirectory => SourceDirectory / "qyl.sdk.aspnetcore";

    AbsolutePath DemoDirectory => SourceDirectory / "qyl.demo";

    AbsolutePath TestsDirectory => RootDirectory / "tests";

    AbsolutePath UnitTestsDirectory => TestsDirectory / "UnitTests";

    AbsolutePath IntegrationTestsDirectory => TestsDirectory / "IntegrationTests";

    AbsolutePath ExamplesDirectory => RootDirectory / "examples";

    AbsolutePath AspNetCoreExampleDirectory => ExamplesDirectory / "qyl.AspNetCore.Example";

    AbsolutePath WebUiDirectory => DashboardDirectory;

    AbsolutePath GetSolutionPath() =>
        Solution.Path ?? RootDirectory.GlobFiles("*.sln", "*.slnx").FirstOrDefault()
        ?? throw new InvalidOperationException("Unable to locate solution file");
}
