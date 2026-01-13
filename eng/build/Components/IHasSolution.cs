using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;

namespace Components;

interface IHasSolution : INukeBuild
{
    [Solution(GenerateProjects = true)] Solution Solution => TryGetValue(() => Solution)!;

    // Build output directories
    AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";
    AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";
    AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";

    // Source directories (actual projects)
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath CollectorDirectory => SourceDirectory / "qyl.collector";
    AbsolutePath DashboardDirectory => SourceDirectory / "qyl.dashboard";
    AbsolutePath McpServerDirectory => SourceDirectory / "qyl.mcp";
    AbsolutePath ProtocolDirectory => SourceDirectory / "qyl.protocol";

    // Test directories
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath CollectorTestsDirectory => TestsDirectory / "qyl.collector.tests";

    // Examples
    AbsolutePath ExamplesDirectory => RootDirectory / "examples";

    // Docker Compose
    AbsolutePath ComposeFile => SourceDirectory / "compose.yaml";

    // Alias for dashboard (used by frontend targets)
    AbsolutePath WebUiDirectory => DashboardDirectory;

    AbsolutePath GetSolutionPath() =>
        Solution.Path ?? RootDirectory.GlobFiles("*.sln", "*.slnx").FirstOrDefault()
        ?? throw new InvalidOperationException("Unable to locate solution file");
}