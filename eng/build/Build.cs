using Components;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Serilog;

/// <summary>
///     NUKE Build composition root for Paperless.Telemetry.
///     All targets are inherited from interface-based build components.
/// </summary>
/// <remarks>
///     Components:
///     - IHasSolution: Shared Solution, paths for dashboard.Receiver, dashboard.web, lol
///     - IRestore: NuGet package restore
///     - ICompile: Build with GitVersion
///     - ITest: xUnit v3 / MTP testing
///     - ICoverage: Code coverage with ReportGenerator
///     - IChangelog: Git-based changelog generation
///     - IDockerBuild: dashboard.Receiver and dashboard.web image builds (parallel)
///     - IDockerCompose: Docker Compose orchestration
///     - ITestContainers: CI Docker configuration
///     - IFrontend: dashboard.web React SPA build (npm/Vite/Vitest)
///     - IKiota: TypeScript API client generation (optional, requires OpenAPI spec)
/// </remarks>
[GitHubActions(
	"ci",
	GitHubActionsImage.UbuntuLatest,
	AutoGenerate = false,
	OnPushBranches = ["main", "develop", "feature/*"],
	OnPullRequestBranches = ["main", "develop"],
	InvokedTargets = [nameof(ITest.Test)],
	FetchDepth = 0)]
internal sealed class Build : NukeBuild,
	// Core build pipeline
	IRestore,
	ICompile,
	IChangelog,
	// Testing
	ITest,
	ICoverage,
	ITestContainers,
	// Docker
	IDockerBuild,
	IDockerCompose,
	// Frontend
	IFrontend,
	IKiota
{
	/// <summary>Entry point - default target is Test.</summary>
	public static int Main() => Execute<Build>(x => ((ITest)x).Test);

	/// <summary>Print build info at start.</summary>
	Target Print => d => d
		.Unlisted()
		.Before<ICompile>(x => x.Clean)
		.Executes(() =>
		{
			var compile = (ICompile)this;
			var gitVersion = compile.GitVersion;

			Log.Information("═══════════════════════════════════════════════════════════════");
			Log.Information("  Paperless.Telemetry Build");
			Log.Information("═══════════════════════════════════════════════════════════════");
			Log.Information("  Configuration : {Configuration}", compile.Configuration);
			Log.Information("  Version       : {Version}", gitVersion?.FullSemVer ?? "N/A");
			Log.Information("  Branch        : {Branch}", gitVersion?.BranchName ?? "N/A");
			Log.Information("  Commit        : {Sha}", gitVersion?.Sha?[..8] ?? "N/A");
			Log.Information("  Solution      : {Solution}", ((IHasSolution)this).Solution.FileName);
			Log.Information("  IsServerBuild : {IsServer}", IsServerBuild);
			Log.Information("═══════════════════════════════════════════════════════════════");
		});

	/// <summary>Full CI pipeline: Clean → Compile → Test → Coverage.</summary>
	Target Ci => d => d
		.Description("Full CI pipeline (backend only)")
		.DependsOn<ICompile>(x => x.Clean)
		.DependsOn<ICoverage>(x => x.Coverage)
		.Executes(() =>
		{
			Log.Information("Backend CI pipeline completed successfully");
		});

	/// <summary>Full CI pipeline including frontend.</summary>
	Target Full => d => d
		.Description("Full CI pipeline (backend + frontend)")
		.DependsOn<ICompile>(x => x.Clean)
		.DependsOn<ICoverage>(x => x.Coverage)
		.TryDependsOn<IKiota>(x => x.Kiota) // Optional - only runs if OpenAPI spec exists
		.DependsOn<IFrontend>(x => x.FrontendBuild)
		.DependsOn<IFrontend>(x => x.FrontendTest)
		.DependsOn<IFrontend>(x => x.FrontendLint)
		.Executes(() =>
		{
			Log.Information("Full CI pipeline completed successfully");
			Log.Information("  Backend:  ✓ Compiled, tested, coverage");
			Log.Information("  Frontend: ✓ Built, tested, linted");
		});

	/// <summary>Start full stack for development.</summary>
	Target Dev => d => d
		.Description("Start development environment (Docker + compile)")
		.DependsOn<IDockerCompose>(x => x.DockerUp)
		.DependsOn<ICompile>(x => x.Compile)
		.Executes(() =>
		{
			Log.Information("Development environment ready");
			Log.Information("  Receiver: http://localhost:5000 (REST API)");
			Log.Information("  OTLP:     http://localhost:4317 (gRPC), :4318 (HTTP)");
			Log.Information("  web:      http://localhost:3000");
			Log.Information("  Frontend: Run 'nuke frontend-dev' in another terminal");
		});
}