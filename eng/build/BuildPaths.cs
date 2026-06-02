
using System;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Components;

namespace Qyl.Build;


interface IHazSourcePaths : IHazSolution, IHazArtifacts
{
    new AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";
    AbsolutePath ServicesDirectory => RootDirectory / "services";
    AbsolutePath PackagesDirectory => RootDirectory / "packages";
    AbsolutePath InternalDirectory => RootDirectory / "internal";
    AbsolutePath CollectorDirectory => ServicesDirectory / "qyl.collector";
    AbsolutePath DashboardDirectory => ServicesDirectory / "qyl.dashboard";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ComposeFile => RootDirectory / "eng" / "compose.yaml";
    AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";
    AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";
}


sealed record CodegenPaths(AbsolutePath Root)
{
    AbsolutePath Collector => Root / "services" / "qyl.collector";
    public AbsolutePath CollectorStorage => Collector / "Storage";

    public static CodegenPaths From(INukeBuild build) => new(build.RootDirectory);
}


interface IVersionize : IHazSourcePaths
{
    [PathVariable]
    Tool Versionize => TryGetValue(() => Versionize)
                       ?? throw new InvalidOperationException(
                           "Versionize tool not found. Install: dotnet tool install -g Versionize");

    Target Changelog => d => d
        .Unlisted()
        .Description("Generate CHANGELOG from conventional commits (Release runs this)")
        .Executes(() => Versionize("--dry-run", RootDirectory));

    Target Release => d => d
        .Description("Bump version, update CHANGELOG, create tag")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => Versionize(null, RootDirectory));
}
