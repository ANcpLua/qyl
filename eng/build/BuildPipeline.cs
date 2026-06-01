
using System;
using System.IO;
using System.Runtime.InteropServices;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Serilog;

namespace Qyl.Build;

[ParameterPrefix(nameof(IPipeline))]
interface IPipeline : IHazSourcePaths
{
    AbsolutePath SemconvDirectory => RootDirectory / "eng" / "semconv";
    AbsolutePath DashboardDistDirectory => DashboardDirectory / "dist";

    // The TypeSpec pipeline (TypeSpecInstall/TypeSpecCompile + the `Generate` aggregate) was
    // removed: core/specs is gone, REST clients now generate on demand via Scalar.Kiota.Extension
    // (services/qyl.collector), and OTel semantic-convention constants ship as external NuGet
    // packages (Qyl.OpenTelemetry.SemanticConventions{,.Incubating}). The Roslyn source generators
    // under internal/*.generators still run at compile time.
    //
    // FOLLOW-UP: OtelConventions + eng/semconv/scripts/run-weaver.sh still target several deleted
    // paths (core/specs/.../otel-attribute-registry.json, packages/qyl-client, docs/attributes) and
    // also rewrite the now-external OTel packages. It only runs on explicit `nuke OtelConventions`,
    // so it is non-blocking, but it needs a dedicated pass to drop the dead destinations and the
    // external-OTel output, keeping only the still-local qyl-owned Qyl.SemanticConventions.
    Target OtelConventions => d => d
        .Unlisted()
        .Description("Weaver → semconv.ts + C# OTel/qyl packages (idempotent)")
        .OnlyWhenStatic(() => SemconvDirectory.DirectoryExists())
        .Executes(() =>
        {
            ProcessTasks.StartProcess("git", "submodule update --init .tools/semconv-upstream",
                RootDirectory, logOutput: true).AssertZeroExitCode();

            var bootstrap = SemconvDirectory / "bootstrap-weaver.sh";
            var script = SemconvDirectory / "run-weaver.sh";
            if (!script.FileExists())
                throw new FileNotFoundException($"run-weaver.sh not found at {script}");
            ProcessTasks.StartProcess("bash", bootstrap, logOutput: true).AssertZeroExitCode();
            ProcessTasks.StartProcess("bash", script, logOutput: true).AssertZeroExitCode();

            var weaverArch = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "aarch64-apple-darwin"
                    : "x86_64-apple-darwin"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                        ? "aarch64-pc-windows-msvc"
                        : "x86_64-pc-windows-msvc"
                    : RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                        ? "aarch64-unknown-linux-gnu"
                        : "x86_64-unknown-linux-gnu";
            var weaverExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "weaver.exe" : "weaver";
            var weaverBin = RootDirectory / ".tools" / "weaver" / $"weaver-{weaverArch}" / weaverExecutable;

            var templatesDir = SemconvDirectory / "templates" / "registry";
            var upstreamModel = RootDirectory / ".tools" / "semconv-upstream" / "model";
            var qylModel = SemconvDirectory / "model" / "qyl";

            RunWeaver(weaverBin, upstreamModel, templatesDir, "csharp_stable",
                RootDirectory / "packages" / "Qyl.OpenTelemetry.SemanticConventions");
            RunWeaver(weaverBin, upstreamModel, templatesDir, "csharp_incubating",
                RootDirectory / "packages" / "Qyl.OpenTelemetry.SemanticConventions.Incubating");
            RunWeaver(weaverBin, qylModel, templatesDir, "csharp_qyl",
                RootDirectory / "packages" / "Qyl.SemanticConventions");

            var schemaSource = RootDirectory / ".tools" / "semconv-upstream" / "schemas" / "1.41.0";
            if (File.Exists(schemaSource))
            {
                ReadOnlySpan<string> otelPackages =
                    ["Qyl.OpenTelemetry.SemanticConventions", "Qyl.OpenTelemetry.SemanticConventions.Incubating"];
                foreach (var pkg in otelPackages)
                {
                    var schemasDir = RootDirectory / "packages" / pkg / "schemas";
                    Directory.CreateDirectory(schemasDir);
                    File.Copy(schemaSource, schemasDir / "1.41.0.yaml", true);
                }
            }

            static void RunWeaver(AbsolutePath weaver, AbsolutePath registry,
                AbsolutePath templates, string templateSet, AbsolutePath outputDir)
            {
                Directory.CreateDirectory(outputDir);
                ProcessTasks.StartProcess(weaver,
                    $"registry generate --registry \"{registry}\" --templates \"{templates}\" {templateSet} \"{outputDir}\"",
                    logOutput: true).AssertZeroExitCode();
                Log.Information("OtelConventions: {Template} → {Output}", templateSet, outputDir);
            }
        });

    Target FrontendInstall => d => d
        .Unlisted()
        .Description("npm install in services/qyl.dashboard")
        .Executes(() => NpmTasks.NpmInstall(s => s
            .SetProcessWorkingDirectory<NpmInstallSettings>(DashboardDirectory)));

    Target FrontendDev => d => d
        .Description("Run the Vite dev server (hot reload at http://localhost:5173)")
        .DependsOn(FrontendInstall)
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("dev")));

    Target FrontendBuild => d => d
        .Description("Build the frontend for production (tsc + vite build)")
        .DependsOn(FrontendInstall)
        .Produces(DashboardDistDirectory / "**/*")
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("build")));

    Target FrontendTest => d => d
        .Unlisted()
        .Description("Run frontend tests (Vitest)")
        .DependsOn(FrontendInstall)
        .Executes(() => NpmTasks.NpmRun(s => s
            .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
            .SetCommand("test")
            .SetArguments("--", "--run")));

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
