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
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath CollectorDirectory => SourceDirectory / "qyl.collector";
    AbsolutePath DashboardDirectory => SourceDirectory / "qyl.dashboard";
    AbsolutePath McpServerDirectory => SourceDirectory / "qyl.mcp";
    AbsolutePath ProtocolDirectory => SourceDirectory / "qyl.protocol";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ExamplesDirectory => RootDirectory / "examples";
    AbsolutePath ComposeFile => SourceDirectory / "compose.yaml";
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
    public AbsolutePath Protocol => Root / "src" / "qyl.protocol";
    public AbsolutePath ProtocolModels => Protocol / "Models";
    public AbsolutePath ProtocolPrimitives => Protocol / "Primitives";
    public AbsolutePath ProtocolAttributes => Protocol / "Attributes";
    public AbsolutePath ProtocolContracts => Protocol / "Contracts";
    public AbsolutePath Collector => Root / "src" / "qyl.collector";
    public AbsolutePath CollectorStorage => Collector / "Storage";
    public AbsolutePath Dashboard => Root / "src" / "qyl.dashboard";
    public AbsolutePath DashboardTypes => Dashboard / "src" / "types" / "generated";
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
        .DependsOn<Nuke.Components.ICompile>(static x => x.Compile)
        .Executes(() => Versionize(null, RootDirectory));
}
