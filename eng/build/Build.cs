using Components;
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
    IDockerBuild,
    IDockerCompose,
    IFrontend,
    ITypeSpec,
    IGenerate
{
    Target Print => d => d
        .Unlisted()
        .Before<ICompile>(x => x.Clean)
        .Executes(() =>
        {
            var gitVersion = ((ICompile)this).GitVersion;

            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  qyl. Build - AI Observability Platform");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Configuration : {Configuration}", ((ICompile)this).Configuration);
            Log.Information("  Version       : {Version}", gitVersion?.FullSemVer ?? "N/A");
            Log.Information("  Branch        : {Branch}", gitVersion?.BranchName ?? "N/A");
            Log.Information("  Commit        : {Sha}", gitVersion?.Sha?[..8] ?? "N/A");
            Log.Information("  Solution      : {Solution}", ((IHasSolution)this).Solution.FileName);
            Log.Information("  IsServerBuild : {IsServer}", IsServerBuild);
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    Target Ci => d => d
        .Description("Full CI pipeline (backend only)")
        .DependsOn<ICompile>(x => x.Clean)
        .DependsOn<ICoverage>(x => x.Coverage)
        .Executes(() => { Log.Information("Backend CI pipeline completed successfully"); });

    Target Full => d => d
        .Description("Full CI pipeline (backend + frontend + TypeSpec)")
        .DependsOn<ICompile>(x => x.Clean)
        .DependsOn<ICoverage>(x => x.Coverage)
        .DependsOn<ITypeSpec>(x => x.TypeSpecCompile)
        .DependsOn<IFrontend>(x => x.FrontendBuild)
        .DependsOn<IFrontend>(x => x.FrontendTest)
        .DependsOn<IFrontend>(x => x.FrontendLint)
        .Executes(() =>
        {
            Log.Information("Full CI pipeline completed successfully");
            Log.Information("  Backend:  ✓ Compiled, tested, coverage");
            Log.Information("  TypeSpec: ✓ Compiled to OpenAPI");
            Log.Information("  Frontend: ✓ Built, tested, linted");
        });

    Target Dev => d => d
        .Description("Start development environment (Docker + compile)")
        .DependsOn<IDockerCompose>(x => x.DockerUp)
        .DependsOn<ICompile>(x => x.Compile)
        .Executes(() =>
        {
            Log.Information("Development environment ready");
            Log.Information("  Collector:  http://localhost:5100 (REST API + SSE)");
            Log.Information("  Dashboard:  http://localhost:5173 (Vite dev server)");
            Log.Information("  MCP:        http://localhost:5100/mcp (AI agent queries)");
            Log.Information("");
            Log.Information("  Run 'nuke frontend-dev' in another terminal for hot reload");
        });

    public static int Main() => Execute<Build>(x => ((ITest)x).Test);
}