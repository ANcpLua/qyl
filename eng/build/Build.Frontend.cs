using Components;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Serilog;

[ParameterPrefix(nameof(IFrontend))]
internal interface IFrontend : IHasSolution
{
    AbsolutePath DashboardDistDirectory => DashboardDirectory / "dist";

    AbsolutePath NodeModulesDirectory => DashboardDirectory / "node_modules";

    Target FrontendInstall => d => d
        .Description("Install frontend npm dependencies")
        .Executes(() =>
        {
            Log.Information("Installing npm dependencies in {Directory}", DashboardDirectory);

            NpmTasks.NpmInstall(s => s
                .SetProcessWorkingDirectory<NpmInstallSettings>(DashboardDirectory));
        });

    Target FrontendBuild => d => d
        .Description("Build frontend for production (tsc + vite build)")
        .DependsOn<IFrontend>(x => x.FrontendInstall)
        .Produces(DashboardDistDirectory / "**/*")
        .Executes(() =>
        {
            Log.Information("Building qyl.dashboard...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("build"));

            Log.Information("Dashboard built → {Output}", DashboardDistDirectory);
        });

    Target FrontendTest => d => d
        .Description("Run frontend tests (Vitest)")
        .DependsOn<IFrontend>(x => x.FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Running frontend tests...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("test")
                .SetArguments("--", "--run"));
        });

    Target FrontendCoverage => d => d
        .Description("Run frontend tests with coverage")
        .DependsOn<IFrontend>(x => x.FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Running frontend tests with coverage...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("test:coverage"));
        });

    Target FrontendLint => d => d
        .Description("Lint frontend code (ESLint)")
        .DependsOn<IFrontend>(x => x.FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Linting frontend code...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("lint"));
        });

    Target FrontendLintFix => d => d
        .Description("Fix frontend linting issues")
        .DependsOn<IFrontend>(x => x.FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Fixing frontend linting issues...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("lint:fix"));
        });

    Target FrontendDev => d => d
        .Description("Start frontend dev server (Vite)")
        .DependsOn<IFrontend>(x => x.FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Starting Vite dev server for qyl.dashboard...");
            Log.Information("  URL: http://localhost:5173");
            Log.Information("  API: Proxied to http://localhost:5100 (override with VITE_API_URL)");
            Log.Information("  Press Ctrl+C to stop");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("dev"));
        });

    Target FrontendClean => d => d
        .Description("Clean frontend build artifacts")
        .Executes(() =>
        {
            Log.Information("Cleaning frontend artifacts...");
            DashboardDistDirectory.DeleteDirectory();
            Log.Information("Cleaned: {Directory}", DashboardDistDirectory);
        });

    Target FrontendTypeCheck => d => d
        .Description("Type check frontend (tsc --noEmit)")
        .DependsOn<IFrontend>(x => x.FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Type checking frontend...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("typecheck"));
        });
}
