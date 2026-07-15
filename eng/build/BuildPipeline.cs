
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
        .Description("Install every first-party frontend from its lock file")
        .Executes(() =>
        {
            foreach (var directory in FrontendDirectories)
                ProcessTasks.StartProcess("npm", "ci", directory, logOutput: true)
                    .AssertZeroExitCode();
        });

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
        .Executes(() =>
        {
            foreach (var directory in FrontendDirectories)
                NpmTasks.NpmRun(s => s
                    .SetProcessWorkingDirectory<NpmRunSettings>(directory)
                    .SetCommand("build"));
        });

    Target FrontendTest => d => d
        .Unlisted()
        .Description("Run frontend tests (Vitest)")
        .DependsOn(FrontendInstall)
        .Executes(() =>
        {
            foreach (var directory in FrontendDirectories)
                ProcessTasks.StartProcess("npm", "test -- --run", directory, logOutput: true)
                    .AssertZeroExitCode();
        });

    Target FrontendE2E => d => d
        .Description("Exercise the embedded Release collector, dashboard, product API, and OTLP routes")
        .DependsOn(FrontendBuild)
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("e2e")));

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
            foreach (var directory in FrontendDirectories)
            {
                var dist = directory / "dist";
                dist.DeleteDirectory();
                Log.Information("Cleaned: {Directory}", dist);
            }
        });
}
