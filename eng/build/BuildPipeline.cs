using System;
using System.Collections.Generic;
using System.Linq;

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Components;
using Serilog;

namespace Qyl.Build;

[ParameterPrefix(nameof(IPipeline))]
interface IPipeline : IHazSourcePaths
{
    AbsolutePath DashboardDistDirectory => DashboardDirectory / "dist";

    /// <summary>Playwright colours its report; NO_COLOR from the outer shell would strip it.</summary>
    static IReadOnlyDictionary<string, string> DashboardEnvironmentWithoutNoColor() =>
        Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .Where(static e => !string.Equals((string)e.Key, "NO_COLOR", StringComparison.Ordinal))
            .ToDictionary(static e => (string)e.Key, static e => (string?)e.Value ?? string.Empty);

    Target FrontendInstall => d => d
        .Unlisted()
        .Description("Install the product dashboard from its lock file")
        .Executes(() => ProcessTasks.StartProcess("bun", "install --frozen-lockfile", DashboardDirectory, logOutput: true)
            .AssertZeroExitCode());

    Target FrontendDev => d => d
        .Description("Run the Vite dev server (hot reload at http://localhost:5173)")
        .DependsOn(FrontendInstall)
        .Executes(() => ProcessTasks.StartProcess("bun", "run dev", DashboardDirectory, logOutput: true)
            .AssertZeroExitCode());

    Target FrontendBuild => d => d
        .Description("Build the frontend for production (tsc + vite build)")
        .DependsOn(FrontendInstall)
        .Before<ICompile>(static x => x.Compile)
        .Produces(DashboardDistDirectory / "**/*")
        .Executes(() => ProcessTasks.StartProcess("bun", "run build", DashboardDirectory, logOutput: true)
            .AssertZeroExitCode());

    Target FrontendTest => d => d
        .Unlisted()
        .Description("Run frontend tests (Vitest)")
        .DependsOn(FrontendInstall)
        .Executes(() => ProcessTasks.StartProcess("bun", "run test -- --run", DashboardDirectory, logOutput: true)
            .AssertZeroExitCode());

    Target FrontendE2E => d => d
        .Description("Exercise the embedded Release collector, dashboard, product API, and OTLP routes")
        .DependsOn(FrontendBuild)
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => ProcessTasks.StartProcess("bun", "run e2e", DashboardDirectory, environmentVariables: DashboardEnvironmentWithoutNoColor(), logOutput: true)
            .AssertZeroExitCode());

    Target FrontendLint => d => d
        .Unlisted()
        .Description("Lint frontend (oxlint)")
        .DependsOn(FrontendInstall)
        .Executes(() => ProcessTasks.StartProcess("bun", "run lint", DashboardDirectory, logOutput: true)
            .AssertZeroExitCode());

    Target FrontendClean => d => d
        .Unlisted()
        .Description("Clean frontend build artifacts")
        .Executes(() =>
        {
            DashboardDistDirectory.DeleteDirectory();
            Log.Information("Cleaned: {Directory}", DashboardDistDirectory);
        });
}
