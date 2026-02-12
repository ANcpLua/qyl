// =============================================================================
// qyl Build System - Entry Point
// =============================================================================
// NUKE build orchestration for AI Observability Platform
// Single entry: nuke <target> or ./eng/build.sh <target>
// =============================================================================

using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Components;
using Serilog;

[GitHubActions(
    "ci",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = false,
    OnPushBranches = ["main", "develop", "feature/*"],
    OnPullRequestBranches = ["main", "develop"],
    InvokedTargets = ["Test"],
    FetchDepth = 0)]
sealed class Build : NukeBuild,
    IQylTest,
    ICoverage,
    IVersionize,
    IDocker,
    IPipeline,
    IVerify
{
    // ── GitVersion (net10.0, not net8.0) ────────────────────────────────────────
    // NOT exposed via IHazGitVersion — NUKE's ICompile.ReportSummary NREs when
    // GitVersion injection fails silently. Accessed directly where needed.
    [GitVersion(Framework = "net10.0", NoCache = true, NoFetch = true)]
    internal readonly GitVersion? Versioning;

    // ── IHazArtifacts override (Artifacts/, not output/) ─────────────────────
    AbsolutePath IHazArtifacts.ArtifactsDirectory => RootDirectory / "Artifacts";

    // ── ICompile.CompileSettings override (CI flags) ─────────────────────────
    Configure<DotNetBuildSettings> ICompile.CompileSettings => s => s
        .SetDeterministic(IsServerBuild)
        .SetContinuousIntegrationBuild(IsServerBuild);

    // ── Helper to avoid ((IFoo)this) casting ─────────────────────────────────
    T From<T>() where T : INukeBuild => (T)(object)this;

    // ════════════════════════════════════════════════════════════════════════════
    // Orchestration Targets
    // ════════════════════════════════════════════════════════════════════════════

    Target Clean => d => d
        .Before<IRestore>(static x => x.Restore)
        .Executes(() =>
        {
            RootDirectory.GlobDirectories("**/bin", "**/obj").DeleteDirectories();
            From<IHazArtifacts>().ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Print => d => d
        .Unlisted()
        .Before<Nuke.Components.ICompile>(static x => x.Compile)
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
        .Description("Full CI pipeline (backend only)")
        .DependsOn(Clean)
        .DependsOn<ICoverage>(static x => x.Coverage);

    Target Full => d => d
        .Description("Full CI pipeline (backend + frontend + TypeSpec + verification)")
        .DependsOn(Clean)
        .DependsOn<ICoverage>(static x => x.Coverage)
        .DependsOn<IPipeline>(static x => x.Generate)
        .DependsOn<IVerify>(static x => x.Verify)
        .DependsOn<IPipeline>(static x => x.FrontendBuild)
        .DependsOn<IPipeline>(static x => x.FrontendTest)
        .DependsOn<IPipeline>(static x => x.FrontendLint);

    Target Dev => d => d
        .Description("Start development environment (Docker + compile)")
        .DependsOn<IDocker>(static x => x.DockerUp)
        .DependsOn<Nuke.Components.ICompile>(static x => x.Compile)
        .Executes(static () =>
        {
            Log.Information("Development environment ready");
            Log.Information("  Collector:  http://localhost:5100 (REST API + SSE)");
            Log.Information("  Dashboard:  http://localhost:5173 (Vite dev server)");
            Log.Information("  MCP:        http://localhost:5100/mcp (AI agent queries)");
            Log.Information("  Run 'nuke FrontendDev' in another terminal for hot reload");
        });

    public static int Main() => Execute<Build>(static x => ((IQylTest)x).Test);
}
