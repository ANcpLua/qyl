// =============================================================================
// qyl Build System - Core Components
// =============================================================================
// Foundation: Solution, paths, compilation, versioning
// =============================================================================


// ════════════════════════════════════════════════════════════════════════════════
// CONFIGURATION
// ════════════════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Serilog;

[TypeConverter(typeof(TypeConverter<Configuration>))]
sealed class Configuration : Enumeration
{
    public static readonly Configuration Debug = new()
    {
        Value = nameof(Debug)
    };

    public static readonly Configuration Release = new()
    {
        Value = nameof(Release)
    };

    public static implicit operator string(Configuration c) => c.Value;
}

// ════════════════════════════════════════════════════════════════════════════════
// IHasSolution - Base interface for all build components
// ════════════════════════════════════════════════════════════════════════════════

interface IHasSolution : INukeBuild
{
    [Solution(GenerateProjects = true)]
    Solution Solution => TryGetValue(() => Solution)
                         ?? throw new InvalidOperationException(
                             "Solution not available. Ensure the build is properly initialized.");

    // ─── Build Artifacts ────────────────────────────────────────────────────
    AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";
    AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";
    AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";

    // ─── Source Projects ────────────────────────────────────────────────────
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath CollectorDirectory => SourceDirectory / "qyl.collector";
    AbsolutePath DashboardDirectory => SourceDirectory / "qyl.dashboard";
    AbsolutePath McpServerDirectory => SourceDirectory / "qyl.mcp";
    AbsolutePath ProtocolDirectory => SourceDirectory / "qyl.protocol";

    // ─── Tests ──────────────────────────────────────────────────────────────
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath CollectorTestsDirectory => TestsDirectory / "qyl.collector.tests";

    // ─── Other ──────────────────────────────────────────────────────────────
    AbsolutePath ExamplesDirectory => RootDirectory / "examples";
    AbsolutePath ComposeFile => SourceDirectory / "compose.yaml";
    AbsolutePath WebUiDirectory => DashboardDirectory;

    AbsolutePath GetSolutionPath() =>
        Solution.Path ?? RootDirectory.GlobFiles("*.sln", "*.slnx").FirstOrDefault()
        ?? throw new InvalidOperationException("Unable to locate solution file");
}

// ════════════════════════════════════════════════════════════════════════════════
// BuildPaths - Centralized path constants
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
///     Single source of truth for all build paths.
///     Use <see cref="From" /> to create from a NUKE build instance.
/// </summary>
public sealed record BuildPaths(AbsolutePath Root)
{
    // ─── Core (God Schema - Single Source of Truth) ─────────────────────────
    public AbsolutePath Core => Root / "core";
    public AbsolutePath TypeSpec => Core / "specs";
    public AbsolutePath OpenApi => Core / "openapi";

    // ─── Generated Outputs ──────────────────────────────────────────────────
    public AbsolutePath Generated => Core / "generated";
    public AbsolutePath GeneratedOpenApi => Generated / "openapi";
    public AbsolutePath GeneratedCSharp => Generated / "csharp";
    public AbsolutePath GeneratedDuckDb => Generated / "duckdb";
    public AbsolutePath GeneratedTypeScript => Generated / "typescript";

    // ─── Source Projects ────────────────────────────────────────────────────
    public AbsolutePath Src => Root / "src";
    public AbsolutePath Protocol => Src / "qyl.protocol";
    public AbsolutePath ProtocolModels => Protocol / "Models";
    public AbsolutePath ProtocolPrimitives => Protocol / "Primitives";
    public AbsolutePath ProtocolAttributes => Protocol / "Attributes";
    public AbsolutePath ProtocolContracts => Protocol / "Contracts";
    public AbsolutePath Collector => Src / "qyl.collector";
    public AbsolutePath CollectorStorage => Collector / "Storage";
    public AbsolutePath Dashboard => Src / "qyl.dashboard";
    public AbsolutePath DashboardSrc => Dashboard / "src";
    public AbsolutePath DashboardTypes => DashboardSrc / "types" / "generated";
    public AbsolutePath Mcp => Src / "qyl.mcp";

    // ─── Build Artifacts ────────────────────────────────────────────────────
    public AbsolutePath Artifacts => Root / "Artifacts";
    public AbsolutePath TestResults => Root / "TestResults";
    public AbsolutePath Coverage => Artifacts / "coverage";

    // ─── Tests & Examples ───────────────────────────────────────────────────
    public AbsolutePath Tests => Root / "tests";
    public AbsolutePath CollectorTests => Tests / "qyl.collector.tests";
    public AbsolutePath Examples => Root / "examples";

    // ─── Engineering ────────────────────────────────────────────────────────
    public AbsolutePath Eng => Root / "eng";
    public AbsolutePath EngBuild => Eng / "build";
    public AbsolutePath EngMsBuild => Eng / "MSBuild";

    // ─── Factory ────────────────────────────────────────────────────────────
    public static BuildPaths From(INukeBuild build) => new(build.RootDirectory);
}

// namespace Context

// ════════════════════════════════════════════════════════════════════════════════
// ICompile - .NET Build Targets
// ════════════════════════════════════════════════════════════════════════════════

[ParameterPrefix(nameof(ICompile))]
interface ICompile : IHasSolution
{
    [GitVersion(Framework = "net10.0", NoCache = true, NoFetch = true)]
    GitVersion? GitVersion => TryGetValue(() => GitVersion);

    [Parameter("Build configuration (Debug/Release)")]
    Configuration Configuration => TryGetValue(() => Configuration)
                                   ?? (IsLocalBuild ? Configuration.Debug : Configuration.Release);

    Target Restore => d => d
        .Description("Restore NuGet packages")
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore(s => s.SetProjectFile(GetSolutionPath()));
            Log.Information("Restored: {Solution}", Solution.FileName);
        });

    Target Compile => d => d
        .Description("Build the solution")
        .DependsOn(Restore)
        .Executes(() =>
        {
            var settings = new DotNetBuildSettings()
                .SetProjectFile(GetSolutionPath())
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetDeterministic(IsServerBuild)
                .SetContinuousIntegrationBuild(IsServerBuild);

            if (GitVersion is not null)
                settings = settings
                    .SetAssemblyVersion(GitVersion.AssemblySemVer)
                    .SetFileVersion(GitVersion.AssemblySemFileVer)
                    .SetInformationalVersion(GitVersion.InformationalVersion);

            DotNetTasks.DotNetBuild(settings);

            Log.Information("Compiled: {Solution} [{Configuration}]",
                Solution.FileName, Configuration);
        });

    Target Clean => d => d
        .Description("Clean build outputs")
        .Before(Restore)
        .Executes(() =>
        {
            RootDirectory.GlobDirectories("**/bin", "**/obj").DeleteDirectories();
            ArtifactsDirectory.CreateOrCleanDirectory();
            Log.Information("Cleaned: {ArtifactsDirectory}", ArtifactsDirectory);
        });
}

// ════════════════════════════════════════════════════════════════════════════════
// IVersionize - Conventional Commits & Changelog
// ════════════════════════════════════════════════════════════════════════════════

interface IVersionize : IHasSolution
{
    [PathVariable]
    Tool Versionize => TryGetValue(() => Versionize)
                       ?? throw new InvalidOperationException(
                           "Versionize tool not found. Ensure it is installed: dotnet tool install -g Versionize");

    Target Changelog => d => d
        .Description("Generate CHANGELOG from conventional commits")
        .Executes(() => Versionize("--dry-run", RootDirectory));

    Target Release => d => d
        .Description("Bump version, update CHANGELOG, create tag")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => Versionize(null, RootDirectory));
}