// =============================================================================
// qyl Build System - Code Generation Pipeline
// =============================================================================
// TypeSpec native emitters + Weaver (OTel semconv). One command: nuke Generate.
// =============================================================================

using System.IO;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Serilog;

[ParameterPrefix(nameof(IPipeline))]
partial interface IPipeline : IHazSourcePaths
{
    AbsolutePath TypeSpecDirectory => RootDirectory / "core" / "specs";
    AbsolutePath TypeSpecEntry => TypeSpecDirectory / "main.tsp";
    AbsolutePath SemconvDirectory => RootDirectory / "eng" / "semconv";
    AbsolutePath DashboardDistDirectory => DashboardDirectory / "dist";

    Target TypeSpecInstall => d => d
        .Description("npm install in core/specs (--legacy-peer-deps per mandate .npmrc)")
        .OnlyWhenStatic(() => TypeSpecEntry.FileExists())
        .Executes(() => NpmTasks.NpmInstall(s => s
            .SetProcessWorkingDirectory<NpmInstallSettings>(TypeSpecDirectory)
            .AddProcessAdditionalArguments("--legacy-peer-deps")));

    Target TypeSpecCompile => d => d
        .Description("Run six TypeSpec native emitters (csharp + duckdb + ts-types + client-csharp + client-js + json-schema)")
        .DependsOn(TypeSpecInstall)
        .DependsOn(GenerateSemconv)
        .OnlyWhenStatic(() => TypeSpecEntry.FileExists())
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(TypeSpecDirectory)
            .SetCommand("compile")));

    Target GenerateSemconv => d => d
        .Description("Weaver → semconv.ts, promoted-columns.g.sql, otel-attribute-registry.json")
        .OnlyWhenStatic(() => SemconvDirectory.DirectoryExists())
        .Executes(() =>
        {
            var script = SemconvDirectory / "run-weaver.sh";
            var bootstrap = SemconvDirectory / "bootstrap-weaver.sh";
            if (!script.FileExists())
                throw new FileNotFoundException($"run-weaver.sh not found. Run {bootstrap} first.", script);

            ProcessTasks.StartProcess("bash", bootstrap, logOutput: true).AssertZeroExitCode();
            ProcessTasks.StartProcess("bash", script, logOutput: true).AssertZeroExitCode();
        });

    Target Generate => d => d
        .Description("Regenerate ALL code from TypeSpec + Weaver")
        .DependsOn(TypeSpecCompile)
        .DependsOn(GenerateSemconv);

    Target FrontendInstall => d => d
        .Description("npm install in services/qyl.dashboard")
        .Executes(() => NpmTasks.NpmInstall(s => s
            .SetProcessWorkingDirectory<NpmInstallSettings>(DashboardDirectory)));

    Target FrontendBuild => d => d
        .Description("Build frontend for production (tsc + vite build)")
        .DependsOn(FrontendInstall)
        .Produces(DashboardDistDirectory / "**/*")
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("build")));

    Target FrontendTest => d => d
        .Description("Run frontend tests (Vitest)")
        .DependsOn(FrontendInstall)
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("test")
            .SetArguments("--", "--run")));

    Target FrontendLint => d => d
        .Description("Lint frontend (ESLint)")
        .DependsOn(FrontendInstall)
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("lint")));

    Target FrontendClean => d => d
        .Description("Clean frontend build artifacts")
        .Executes(() =>
        {
            DashboardDistDirectory.DeleteDirectory();
            Log.Information("Cleaned: {Directory}", DashboardDistDirectory);
        });
}
