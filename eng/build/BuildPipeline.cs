// =============================================================================
// qyl Build System - Code Generation Pipeline
// =============================================================================
// TypeSpec native emitters + Weaver (OTel semconv). One command: nuke Generate.
// =============================================================================

using System;
using System.IO;
using System.Runtime.InteropServices;
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
        .Description("Weaver → semconv.ts + C# OTel/qyl packages (idempotent)")
        .OnlyWhenStatic(() => SemconvDirectory.DirectoryExists())
        .Executes(() =>
        {
            // 1. Ensure submodule
            ProcessTasks.StartProcess("git", "submodule update --init .tools/semconv-upstream",
                workingDirectory: RootDirectory, logOutput: true).AssertZeroExitCode();

            // 2. Bootstrap Weaver binary + legacy semconv outputs (semconv.ts, SQL, JSON)
            var bootstrap = SemconvDirectory / "bootstrap-weaver.sh";
            var script = SemconvDirectory / "run-weaver.sh";
            if (!script.FileExists())
                throw new FileNotFoundException($"run-weaver.sh not found at {script}");
            ProcessTasks.StartProcess("bash", bootstrap, logOutput: true).AssertZeroExitCode();
            ProcessTasks.StartProcess("bash", script, logOutput: true).AssertZeroExitCode();

            // 3. Determine Weaver binary (bootstrap ensures it exists).
            // Use IsOSPlatform over OSDescription.Contains("Darwin") — OSDescription
            // on newer macOS (26+) may not include the "Darwin" substring.
            var weaverArch = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? (RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "aarch64-apple-darwin" : "x86_64-apple-darwin")
                : "x86_64-unknown-linux-gnu";
            var weaverBin = (AbsolutePath)(RootDirectory / ".tools" / "weaver" / $"weaver-{weaverArch}" / "weaver");

            var templatesDir = SemconvDirectory / "templates" / "registry";
            var upstreamModel = RootDirectory / ".tools" / "semconv-upstream" / "model";
            var qylModel = SemconvDirectory / "model" / "qyl";

            // 4. Generate C# packages
            static void RunWeaver(AbsolutePath weaver, AbsolutePath registry,
                AbsolutePath templates, string templateSet, AbsolutePath outputDir)
            {
                Directory.CreateDirectory(outputDir);
                ProcessTasks.StartProcess(weaver,
                    $"registry generate --registry \"{registry}\" --templates \"{templates}\" {templateSet} \"{outputDir}\"",
                    logOutput: true).AssertZeroExitCode();
                Log.Information("GenerateSemconv: {Template} → {Output}", templateSet, outputDir);
            }

            RunWeaver(weaverBin, upstreamModel, templatesDir, "csharp_stable",
                RootDirectory / "packages" / "Qyl.OpenTelemetry.SemanticConventions");
            RunWeaver(weaverBin, upstreamModel, templatesDir, "csharp_incubating",
                RootDirectory / "packages" / "Qyl.OpenTelemetry.SemanticConventions.Incubating");
            RunWeaver(weaverBin, qylModel, templatesDir, "csharp_qyl",
                RootDirectory / "packages" / "Qyl.SemanticConventions");

            // 5. Copy OTel schema to both OTel packages (embedded resource)
            var schemaSource = RootDirectory / ".tools" / "semconv-upstream" / "schemas" / "1.40.0";
            if (((string)schemaSource).Length > 0 && File.Exists(schemaSource))
            {
                ReadOnlySpan<string> otelPackages = ["Qyl.OpenTelemetry.SemanticConventions", "Qyl.OpenTelemetry.SemanticConventions.Incubating"];
                foreach (var pkg in otelPackages)
                {
                    var schemasDir = RootDirectory / "packages" / pkg / "schemas";
                    Directory.CreateDirectory(schemasDir);
                    File.Copy(schemaSource, schemasDir / "1.40.0.yaml", overwrite: true);
                }
            }
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
