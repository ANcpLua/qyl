
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Components;
using Serilog;

namespace Qyl.Build;

[GitHubActions(
    "ci",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = false,
    OnPushBranches = ["main"],
    OnPullRequestBranches = ["main"],
    InvokedTargets = ["Ci"],
    FetchDepth = 0)]
sealed class Build : NukeBuild,
    ICoverage,
    IVersionize,
    IDocker,
    IPipeline,
    IVerify,
    IPricing,
    ISmoke
{
    [GitVersion(Framework = "net10.0", NoCache = true, NoFetch = true)]
    internal readonly GitVersion? Versioning;


    Target Clean => d => d
        .Before<IRestore>(static x => x.Restore)
        .Executes(() =>
        {
            RootDirectory.GlobDirectories("**/bin", "**/obj").DeleteDirectories();
            From<IHazArtifacts>().ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Print => d => d
        .Unlisted()
        .Before<ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  qyl Build - AI Observability Platform");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Configuration : {Configuration}", From<IHazConfiguration>().Configuration);
            Log.Information("  Version       : {Version}", Versioning?.FullSemVer ?? "N/A");
            Log.Information("  Branch        : {Branch}", Versioning?.BranchName ?? "N/A");
            Log.Information("  Commit        : {Sha}", Versioning?.Sha?[..8] ?? "N/A");
            Log.Information("  Solution      : {Solution}", From<IHazSolution>().Solution.FileName);
            Log.Information("  IsServerBuild : {IsServer}", IsServerBuild);
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    Target Ci => d => d
        .Description("Local CI gate: backend, frontend, and generated artifacts")
        .DependsOn(Clean)
        .DependsOn<ICompile>(static x => x.Compile)
        .DependsOn<IVerify>(static x => x.Verify)
        .DependsOn<IPipeline>(static x => x.FrontendBuild)
        .DependsOn<IPipeline>(static x => x.FrontendLint);

    Target Full => d => d
        .Description("Full local gate: CI plus existing test and smoke targets")
        .DependsOn(Clean)
        .DependsOn<ICompile>(static x => x.Compile)
        .DependsOn<IVerify>(static x => x.Verify)
        .DependsOn<IPipeline>(static x => x.FrontendBuild)
        .DependsOn<IPipeline>(static x => x.FrontendLint)
        .DependsOn<IQylTest>(static x => x.Test)
        .DependsOn<ISmoke>(static x => x.Smoke);

    Target Dev => d => d
        .Description("Start development environment (Docker + compile)")
        .DependsOn<IDocker>(static x => x.DockerUp)
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            Log.Information("Development environment ready ({Version})", Versioning?.FullSemVer ?? "local");
            Log.Information("  Dashboard:  http://localhost:5100");
            Log.Information("  OTLP HTTP:  http://localhost:4318/v1/traces");
            Log.Information("  OTLP gRPC:  http://localhost:4317");
            Log.Information("  Vite Dev:   http://localhost:5173");
            Log.Information("  Run 'nuke FrontendDev' in another terminal for hot reload");
        });

    AbsolutePath IHazArtifacts.ArtifactsDirectory => RootDirectory / "Artifacts";

    Configure<DotNetBuildSettings> ICompile.CompileSettings => s => s
        .SetDeterministic(IsServerBuild)
        .SetContinuousIntegrationBuild(IsServerBuild);

    T From<T>() where T : INukeBuild => (T)(object)this;

    public static int Main() => Execute<Build>(static x => ((ICompile)x).Compile);
}
