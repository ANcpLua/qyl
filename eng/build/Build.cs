
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
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
    ICollectorSemanticCatalog,
    IPricing,
    ISmoke
{
    internal static string VersionLabel => GitScalar("describe --tags --always --dirty", "local");

    internal static string BranchLabel => GitScalar("branch --show-current", "local");

    internal static string CommitLabel => GitScalar("rev-parse --short HEAD", "N/A");


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
            Log.Information("  Version       : {Version}", VersionLabel);
            Log.Information("  Branch        : {Branch}", BranchLabel);
            Log.Information("  Commit        : {Sha}", CommitLabel);
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
            Log.Information(
                "Development environment ready ({Configuration}, {Version})",
                From<IHazConfiguration>().Configuration,
                VersionLabel);
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
