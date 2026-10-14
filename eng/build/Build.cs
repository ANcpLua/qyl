using System;
using System.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Tools.Git;
using Fallout.Components;
using Serilog;

namespace Qyl.Build;

interface IQylTests : IHasSolution, IHasConfiguration
{
    Target Test => d => d
        .Description("Run every .NET test project in the solution using the completed build")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => DotNetTasks.DotNet(
            $"test --solution \"{Solution}\" --configuration {Configuration} --no-build --no-restore --max-parallel-test-modules 1",
            workingDirectory: RootDirectory));
}

/// <summary>
/// Every build, pack and gate of this repository. <c>./eng/build.sh --help</c> lists the targets;
/// <c>Check</c> is the gate before every push (<c>mise run check</c>), <c>CheckFull</c> runs everything
/// (<c>mise run check-full</c>).
/// </summary>
sealed class Build : FalloutBuild,
    ICompile,
    IQylTests,
    ICoverage,
    IDocker,
    IPipeline,
    INativeAot,
    IVerify,
    IDependencyEdges,
    ICliContractLoop,
    ICollectorSemanticCatalog,
    IConfigurationKnobs,
    IHousekeeping,
    IPack,
    IApiSdk,
    IVocabularySmoke
{
    internal static string VersionLabel => GitScalar("describe --tags --always --dirty", "local");

    internal static string BranchLabel => GitScalar("branch --show-current", "local");

    internal static string CommitLabel => GitScalar("rev-parse --short HEAD", "N/A");


    public Target Clean => d => d
        .Before<IRestore>(static x => x.Restore)
        .Before<IPipeline>(static x => x.FrontendInstall)
        .Executes(() =>
        {
            var buildScriptDirectory = (RootDirectory / "eng" / "build")
                .ToString()
                .Replace('\\', '/') + "/";
            var pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            RootDirectory.GlobDirectories("**/bin", "**/obj")
                .Where(directory =>
                {
                    var path = directory.ToString().Replace('\\', '/') + "/";
                    return !path.Contains("/node_modules/", StringComparison.Ordinal)
                           // The running build, its lazy dependencies, and its assets graph live here.
                           && !path.StartsWith(buildScriptDirectory, pathComparison);
                })
                .DeleteDirectories();
            From<IHasArtifacts>().ArtifactsDirectory.CreateOrCleanDirectory();
        });

    public Target Print => d => d
        .Unlisted()
        .Before<ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  qyl Build - AI Observability Platform");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Configuration : {Configuration}", From<IHasConfiguration>().Configuration);
            Log.Information("  Version       : {Version}", VersionLabel);
            Log.Information("  Branch        : {Branch}", BranchLabel);
            Log.Information("  Commit        : {Sha}", CommitLabel);
            Log.Information("  Solution      : {Solution}", From<IHasSolution>().Solution.FileName);
            Log.Information("  IsServerBuild : {IsServer}", IsServerBuild);
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    // Trim and AOT regressions (IL2026/IL3050) are CheckFull's job: Compile builds with the AOT
    // analyzers off; PackSmoke and NativeAot are the gates that see them.
    public Target Check => d => d
        .Description("The gate before every push: compile, tests, verification, SDK and SemConv pins and the dashboard's lint, tests and build")
        .DependsOn<ICompile>(static x => x.Compile)
        .DependsOn<IQylTests>(static x => x.Test)
        .DependsOn<IVerify>(static x => x.Verify)
        .DependsOn<IHousekeeping>(static x => x.VerifySdkVersions)
        .DependsOn<IHousekeeping>(static x => x.VerifySemanticConventionsPin)
        .DependsOn<IPipeline>(static x => x.FrontendBuild)
        .DependsOn<IPipeline>(static x => x.FrontendTest)
        .DependsOn<IPipeline>(static x => x.FrontendLint);

    public Target CheckFull => d => d
        .Description("Everything: Check, NativeAOT, the pack smoke, the SDK proof, the dashboard end-to-end run, the vocabulary smoke and the coverage gate")
        .DependsOn(Check)
        .DependsOn<INativeAot>(static x => x.NativeAot)
        // The pack lane builds with the AOT analyzers Compile disables, so it is the only gate that
        // sees IL2026/IL3050. Omitting it from the full gate is what let a trim regression reach
        // main past a green local run.
        .DependsOn<IPack>(static x => x.PackSmoke)
        // The SDK's own nine-stage proof: it ships from here, so its gates are this gate.
        .DependsOn<IApiSdk>(static x => x.ApiSdk)
        .DependsOn<IPipeline>(static x => x.FrontendE2E)
        .DependsOn<IVocabularySmoke>(static x => x.G1VocabularySmoke)
        .DependsOn<ICoverage>(static x => x.Coverage);

    public Target Dev => d => d
        .Description("Start development environment (Docker + compile)")
        .DependsOn<IDocker>(static x => x.DockerUp)
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            Log.Information(
                "Development environment ready ({Configuration}, {Version})",
                From<IHasConfiguration>().Configuration,
                VersionLabel);
            Log.Information("  Dashboard:  http://localhost:5100");
            Log.Information("  OTLP HTTP:  http://localhost:4318/v1/traces");
            Log.Information("  OTLP gRPC:  http://localhost:4317");
            Log.Information("  Vite Dev:   http://localhost:5173");
            Log.Information("  Run './eng/build.sh FrontendDev' in another terminal for hot reload");
        });

    AbsolutePath IHasArtifacts.ArtifactsDirectory => RootDirectory / "Artifacts";

    Configure<DotNetBuildSettings> ICompile.CompileSettings => s => s
        .SetDeterministic(IsServerBuild)
        .SetContinuousIntegrationBuild(IsServerBuild)
        .SetProperty("QylAot", false);

    T From<T>() where T : IFalloutBuild => (T)(object)this;

    private static string GitScalar(string args, string fallback)
    {
        try
        {
            return GitTasks.Git(args, RootDirectory, logOutput: false, logInvocation: false)
                       .Select(static output => output.Text.Trim())
                       .FirstOrDefault(static text => text.Length > 0)
                   ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static int Main() => Execute<Build>(static x => ((ICompile)x).Compile);
}
