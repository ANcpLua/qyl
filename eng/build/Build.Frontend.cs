using Components;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Serilog;

/// <summary>
///     Frontend build component for React SPA (dashboard.web).
///     Refactored to use NUKE's native NpmTasks.
/// </summary>
/// <remarks>
///     Stack: React 19, TypeScript, Vite 6, Tailwind 4
///     Key improvements over ProcessTasks wrapper:
///     - Type-safe NpmRun with argument handling
///     - Automatic working directory management
///     - Consistent error handling and exit code checking
///     - Verbosity mapping integration
/// </remarks>
[ParameterPrefix(nameof(IFrontend))]
internal interface IFrontend : IHasSolution
{
	// ══════════════════════════════════════════════════════════════════════════
	// PATH CONSTANTS (webUIDirectory inherited from IHasSolution)
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>Frontend build output directory.</summary>
	AbsolutePath WebUiDistDirectory => WebUiDirectory / "dist";

	/// <summary>Frontend node_modules directory.</summary>
	AbsolutePath NodeModulesDirectory => WebUiDirectory / "node_modules";

	// ══════════════════════════════════════════════════════════════════════════
	// TARGETS
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>Install npm dependencies.</summary>
	Target FrontendInstall => d => d
		.Description("Install frontend npm dependencies")
		.Executes(() =>
		{
			Log.Information("Installing npm dependencies in {Directory}", WebUiDirectory);

			NpmTasks.NpmInstall(s => ToolOptionsExtensions
				.SetProcessWorkingDirectory<NpmInstallSettings>(s, WebUiDirectory));
		});

	/// <summary>Build frontend for production.</summary>
	Target FrontendBuild => d => d
		.Description("Build frontend for production (tsc + vite build)")
		.DependsOn<IFrontend>(x => x.FrontendInstall)
		.Produces(WebUiDistDirectory / "**/*")
		.Executes(() =>
		{
			Log.Information("Building frontend...");

			NpmTasks.NpmRun(s => ToolOptionsExtensions
				.SetProcessWorkingDirectory<NpmRunSettings>(s, WebUiDirectory)
				.SetCommand("build"));

			Log.Information("Frontend built → {Output}", WebUiDistDirectory);
		});

	/// <summary>Run frontend tests with Vitest.</summary>
	Target FrontendTest => d => d
		.Description("Run frontend tests (Vitest)")
		.DependsOn<IFrontend>(x => x.FrontendInstall)
		.Executes(() =>
		{
			Log.Information("Running frontend tests...");

			NpmTasks.NpmRun(s => ToolOptionsExtensions
				.SetProcessWorkingDirectory<NpmRunSettings>(s, WebUiDirectory)
				.SetCommand("test")
				.SetArguments("--", "--run"));
		});

	/// <summary>Run frontend tests with coverage.</summary>
	Target FrontendCoverage => d => d
		.Description("Run frontend tests with coverage")
		.DependsOn<IFrontend>(x => x.FrontendInstall)
		.Executes(() =>
		{
			Log.Information("Running frontend tests with coverage...");

			NpmTasks.NpmRun(s => ToolOptionsExtensions
				.SetProcessWorkingDirectory<NpmRunSettings>(s, WebUiDirectory)
				.SetCommand("test:coverage"));
		});

	/// <summary>Lint frontend code with ESLint.</summary>
	Target FrontendLint => d => d
		.Description("Lint frontend code (ESLint)")
		.DependsOn<IFrontend>(x => x.FrontendInstall)
		.Executes(() =>
		{
			Log.Information("Linting frontend code...");

			NpmTasks.NpmRun(s => ToolOptionsExtensions
				.SetProcessWorkingDirectory<NpmRunSettings>(s, WebUiDirectory)
				.SetCommand("lint"));
		});

	/// <summary>Fix linting issues automatically.</summary>
	Target FrontendLintFix => d => d
		.Description("Fix frontend linting issues")
		.DependsOn<IFrontend>(x => x.FrontendInstall)
		.Executes(() =>
		{
			Log.Information("Fixing frontend linting issues...");

			NpmTasks.NpmRun(s => ToolOptionsExtensions
				.SetProcessWorkingDirectory<NpmRunSettings>(s, WebUiDirectory)
				.SetCommand("lint:fix"));
		});

	/// <summary>Start Vite dev server (foreground).</summary>
	Target FrontendDev => d => d
		.Description("Start frontend dev server (Vite)")
		.DependsOn<IFrontend>(x => x.FrontendInstall)
		.Executes(() =>
		{
			Log.Information("Starting Vite dev server...");
			Log.Information("  URL: http://localhost:5173");
			Log.Information("  Press Ctrl+C to stop");

			NpmTasks.NpmRun(s => ToolOptionsExtensions
				.SetProcessWorkingDirectory<NpmRunSettings>(s, WebUiDirectory)
				.SetCommand("dev"));
		});

	/// <summary>Clean frontend build artifacts.</summary>
	Target FrontendClean => d => d
		.Description("Clean frontend build artifacts")
		.Executes(() =>
		{
			Log.Information("Cleaning frontend artifacts...");
			WebUiDistDirectory.DeleteDirectory();
			Log.Information("Cleaned: {Directory}", WebUiDistDirectory);
		});
}