// =============================================================================
// qyl Build System - Path Definitions & Versioning
// =============================================================================
// Foundation: IHazSourcePaths, CodegenPaths, IVersionize
// =============================================================================

using System;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Components;

// ════════════════════════════════════════════════════════════════════════════════
// IHazSourcePaths - qyl project directory structure
// ════════════════════════════════════════════════════════════════════════════════

interface IHazSourcePaths : IHazSolution, IHazArtifacts
{
    new AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";
    AbsolutePath ServicesDirectory => RootDirectory / "services";
    AbsolutePath PackagesDirectory => RootDirectory / "packages";
    AbsolutePath InternalDirectory => RootDirectory / "internal";
    AbsolutePath CollectorDirectory => ServicesDirectory / "qyl.collector";
    AbsolutePath DashboardDirectory => ServicesDirectory / "qyl.dashboard";
    AbsolutePath ProtocolDirectory => PackagesDirectory / "Qyl.Contracts";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ComposeFile => RootDirectory / "docker-compose.yml";
    AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";
    AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";
}

// ════════════════════════════════════════════════════════════════════════════════
// CodegenPaths - Paths for SchemaGenerator and IVerify
// ════════════════════════════════════════════════════════════════════════════════

public sealed record CodegenPaths(AbsolutePath Root)
{
    public AbsolutePath Core => Root / "core";
    public AbsolutePath OpenApi => Core / "openapi";
    public AbsolutePath Protocol => Root / "packages" / "Qyl.Contracts";
    public AbsolutePath Collector => Root / "services" / "qyl.collector";
    public AbsolutePath CollectorObserve => Collector / "Observe";
    public AbsolutePath CollectorStorage => Collector / "Storage";
    public AbsolutePath Migrations => CollectorStorage / "Migrations";
    public AbsolutePath Dashboard => Root / "services" / "qyl.dashboard";
    public AbsolutePath InstrumentationGenerator => Root / "internal" / "qyl.instrumentation.generators";
    public static CodegenPaths From(INukeBuild build) => new(build.RootDirectory);
}

// ════════════════════════════════════════════════════════════════════════════════
// IVersionize - Conventional Commits & Changelog
// ════════════════════════════════════════════════════════════════════════════════

interface IVersionize : IHazSourcePaths
{
    [PathVariable]
    Tool Versionize => TryGetValue(() => Versionize)
                       ?? throw new InvalidOperationException(
                           "Versionize tool not found. Install: dotnet tool install -g Versionize");

    Target Changelog => d => d
        .Description("Generate CHANGELOG from conventional commits")
        .Executes(() => Versionize("--dry-run", RootDirectory));

    Target Release => d => d
        .Description("Bump version, update CHANGELOG, create tag")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => Versionize(null, RootDirectory));
}
