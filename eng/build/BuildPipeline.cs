
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Nuke.Components;
using Serilog;

namespace Qyl.Build;

[ParameterPrefix(nameof(IPipeline))]
interface IPipeline : IHazSourcePaths
{
    AbsolutePath DashboardDistDirectory => DashboardDirectory / "dist";

    Target FrontendInstall => d => d
        .Unlisted()
        .Description("Install the product dashboard from its lock file")
        .Executes(() => ProcessTasks.StartProcess("npm", "ci", DashboardDirectory, logOutput: true)
            .AssertZeroExitCode());

    Target FrontendDev => d => d
        .Description("Run the Vite dev server (hot reload at http://localhost:5173)")
        .DependsOn(FrontendInstall)
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("dev")));

    Target FrontendBuild => d => d
        .Description("Build the frontend for production (tsc + vite build)")
        .DependsOn(FrontendInstall)
        .Before<ICompile>(static x => x.Compile)
        .Produces(DashboardDistDirectory / "**/*")
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("build")));

    Target FrontendTest => d => d
        .Unlisted()
        .Description("Run frontend tests (Vitest)")
        .DependsOn(FrontendInstall)
        .Executes(() => ProcessTasks.StartProcess("npm", "test -- --run", DashboardDirectory, logOutput: true)
            .AssertZeroExitCode());

    Target FrontendE2E => d => d
        .Description("Exercise the embedded Release collector, dashboard, product API, and OTLP routes")
        .DependsOn(FrontendBuild)
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("e2e")
            .RemoveProcessEnvironmentVariable("NO_COLOR")));

    Target FrontendLint => d => d
        .Unlisted()
        .Description("Lint frontend (ESLint)")
        .DependsOn(FrontendInstall)
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("lint")));

    Target FrontendClean => d => d
        .Unlisted()
        .Description("Clean frontend build artifacts")
        .Executes(() =>
        {
            DashboardDistDirectory.DeleteDirectory();
            Log.Information("Cleaned: {Directory}", DashboardDistDirectory);
        });
}
