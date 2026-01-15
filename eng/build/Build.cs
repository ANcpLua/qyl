// =============================================================================
// qyl Build System - Entry Point
// =============================================================================
// NUKE build orchestration for AI Observability Platform
// Single entry: nuke <target> or ./eng/build.sh <target>
// =============================================================================

using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Serilog;

[GitHubActions(
    "ci",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = false,
    OnPushBranches = ["main", "develop", "feature/*"],
    OnPullRequestBranches = ["main", "develop"],
    InvokedTargets = [nameof(ITest.Test)],
    FetchDepth = 0)]
sealed class Build : NukeBuild,
    IVersionize,
    ICoverage,
    IDocker,
    IPipeline,
    IVerify
{
    public static int Main() => Execute<Build>(static x => ((ITest)x).Test);

    // ════════════════════════════════════════════════════════════════════════
    // Orchestration Targets
    // ════════════════════════════════════════════════════════════════════════

    Target Print => d => d
        .Unlisted()
        .Before<ICompile>(static x => x.Clean)
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  qyl Build - AI Observability Platform");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Configuration : {Configuration}", ((ICompile)this).Configuration);
            Log.Information("  Version       : {Version}", ((ICompile)this).GitVersion?.FullSemVer ?? "N/A");
            Log.Information("  Branch        : {Branch}", ((ICompile)this).GitVersion?.BranchName ?? "N/A");
            Log.Information("  Commit        : {Sha}", ((ICompile)this).GitVersion?.Sha?[..8] ?? "N/A");
            Log.Information("  Solution      : {Solution}", ((IHasSolution)this).Solution.FileName);
            Log.Information("  IsServerBuild : {IsServer}", IsServerBuild);
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    Target Ci => d => d
        .Description("Full CI pipeline (backend only)")
        .DependsOn<ICompile>(static x => x.Clean)
        .DependsOn<ICoverage>(static x => x.Coverage)
        .Executes(static () => Log.Information("Backend CI completed"));

    Target Full => d => d
        .Description("Full CI pipeline (backend + frontend + TypeSpec + verification)")
        .DependsOn<ICompile>(static x => x.Clean)
        .DependsOn<ICoverage>(static x => x.Coverage)
        .DependsOn<IPipeline>(static x => x.Generate)
        .DependsOn<IVerify>(static x => x.Verify)
        .DependsOn<IPipeline>(static x => x.FrontendBuild)
        .DependsOn<IPipeline>(static x => x.FrontendTest)
        .DependsOn<IPipeline>(static x => x.FrontendLint)
        .Executes(static () =>
        {
            Log.Information("Full CI completed");
            Log.Information("  Backend:  ✓ Compiled, tested, coverage");
            Log.Information("  TypeSpec: ✓ Compiled to OpenAPI");
            Log.Information("  Codegen:  ✓ Generated + verified");
            Log.Information("  Frontend: ✓ Built, tested, linted");
        });

    Target Dev => d => d
        .Description("Start development environment (Docker + compile)")
        .DependsOn<IDocker>(static x => x.DockerUp)
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(static () =>
        {
            Log.Information("Development environment ready");
            Log.Information("  Collector:  http://localhost:5100 (REST API + SSE)");
            Log.Information("  Dashboard:  http://localhost:5173 (Vite dev server)");
            Log.Information("  MCP:        http://localhost:5100/mcp (AI agent queries)");
            Log.Information("");
            Log.Information("  Run 'nuke FrontendDev' in another terminal for hot reload");
        });
}