using Components;
using Components.Theory;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Serilog;
using IHasSolution = Components.IHasSolution;

[GitHubActions(
    "ci",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = false,
    OnPushBranches = ["main", "develop", "feature/*"],
    OnPullRequestBranches = ["main", "develop"],
    InvokedTargets = [nameof(ITest.Test)],
    FetchDepth = 0)]
sealed class Build : NukeBuild,
    IRestore,
    IChangelog,
    ICoverage,
    ITestContainers,
    IDockerBuild,
    IDockerCompose,
    IFrontend,
    ITypeSpec,
    IEmitter // ← NEW: Code generation emitter
{
    Target Print => d => d
        .Unlisted()
        .Before<ICompile>(x => x.Clean)
        .Executes(() =>
        {
            var compile = (ICompile)this;
            var gitVersion = compile.GitVersion;

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
        .Description("Full CI pipeline (backend + frontend + codegen)")
        .DependsOn<ICompile>(x => x.Clean)
        .DependsOn<IEmitter>(x => x.Emit) // ← Code generation
        .DependsOn<IEmitter>(x => x.SyncGeneratedTypes) // ← Sync to consumers
        .DependsOn<ICoverage>(x => x.Coverage)
        .TryDependsOn<ITypeSpec>(x => x.GenerateAll)
        .DependsOn<IFrontend>(x => x.FrontendBuild)
        .DependsOn<IFrontend>(x => x.FrontendTest)
        .DependsOn<IFrontend>(x => x.FrontendLint)
        .Executes(() =>
        {
            Log.Information("Full CI pipeline completed successfully");
            Log.Information("  Codegen:  ✓ Models, DuckDB schema, TypeScript");
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
        .Description("Generate code and sync to projects")
        .DependsOn<IEmitter>(x => x.Emit)
        .DependsOn<IEmitter>(x => x.SyncGeneratedTypes)
        .TryDependsOn<ITypeSpec>(x => x.GenerateAll)
        .TryDependsOn<ITypeSpec>(x => x.SyncDashboardTypes)
        .Executes(() =>
        {
            Log.Information("Types synced successfully");
            Log.Information("  C# Models:     src/qyl.protocol/Models/");
            Log.Information("  C# Primitives: src/qyl.protocol/Primitives/");
            Log.Information("  DuckDB Schema: src/qyl.collector/Storage/");
            Log.Information("  TypeScript:    src/qyl.dashboard/src/types/generated/");
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

    Target Schema => d => d
        .Description("Show schema summary and validation")
        .DependsOn<IEmitter>(x => x.EmitInfo)
        .Executes(() =>
        {
            var schema = QylSchema.Instance;

            Log.Information("");
            Log.Information("Schema Validation:");
            Log.Information("  Primitives with JSON converters: {Count}",
                schema.Primitives.Count(p => p.JsonConverter is not null));
            Log.Information("  Models with DuckDB columns: {Count}",
                schema.Models.Count(m => m.Properties?.Any(p => p.DuckDbColumn is not null) == true));
            Log.Information("  Promoted gen_ai.* columns: {Count}",
                schema.Models.SelectMany(m => m.Properties ?? []).Count(p => p.IsPromoted));
            Log.Information("  Tables with indexes: {Count}",
                schema.Tables.Count(t => t.Indexes is { Count: > 0 }));
            Log.Information("  Current OTel attributes: {Count}",
                schema.GenAiAttributes.Count(a => !a.Value.IsDeprecated));
            Log.Information("  Deprecated OTel attributes: {Count}",
                schema.GenAiAttributes.Count(a => a.Value.IsDeprecated));
        });

    public static int Main() => Execute<Build>(x => ((ITest)x).Test);
}