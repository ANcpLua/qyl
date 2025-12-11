using Components;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Tools.GitVersion;
using Serilog;

[GitHubActions(
    "ci",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = false,
    OnPushBranches = ["main", "develop", "feature/*"],
    OnPullRequestBranches = ["main", "develop"],
    InvokedTargets = [nameof(ITest.Test)],
    FetchDepth = 0)]
internal sealed class Build : NukeBuild,
    IRestore,
    IChangelog,
    ICoverage,
    ITestContainers,
    IDockerBuild,
    IDockerCompose,
    IFrontend,
    ITypeSpec
{
    Target Print => d => d
        .Unlisted()
        .Before<ICompile>(x => x.Clean)
        .Executes(() =>
        {
            var compile = (ICompile)this;
            GitVersion? gitVersion = compile.GitVersion;

            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  qyl. Build - AI Observability Platform");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Configuration : {Configuration}", compile.Configuration);
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
        .Executes(() =>
        {
            Log.Information("Backend CI pipeline completed successfully");
        });

    Target Full => d => d
        .Description("Full CI pipeline (backend + frontend)")
        .DependsOn<ICompile>(x => x.Clean)
        .DependsOn<ICoverage>(x => x.Coverage)
        .TryDependsOn<ITypeSpec>(x => x.GenerateAll)
        .DependsOn<IFrontend>(x => x.FrontendBuild)
        .DependsOn<IFrontend>(x => x.FrontendTest)
        .DependsOn<IFrontend>(x => x.FrontendLint)
        .Executes(() =>
        {
            Log.Information("Full CI pipeline completed successfully");
            Log.Information("  Backend:  ✓ Compiled, tested, coverage");
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

    Target Sync => d => d
        .Description("Generate clients from TypeSpec and sync to projects")
        .DependsOn<ITypeSpec>(x => x.GenerateAll)
        .DependsOn<ITypeSpec>(x => x.SyncGeneratedTypes)
        .Executes(() =>
        {
            Log.Information("Types synced successfully");
            Log.Information("  TypeScript → src/qyl.dashboard/src/types/generated/");
            Log.Information("  C#         → src/qyl.collector/Generated/");
        });

    Target Demo => d => d
        .Description("Run the qyl demo with telemetry")
        .DependsOn<ICompile>(x => x.Compile)
        .Executes(() =>
        {
            Log.Information("Starting qyl demo...");
            Log.Information("  Make sure qyl.collector is running first!");
            Log.Information("  Run: dotnet run --project src/qyl.demo");
        });

    public static int Main() => Execute<Build>(x => ((ITest)x).Test);
}
