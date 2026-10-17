using System;
using System.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Tools.Git;
using Fallout.Components;
using Serilog;

namespace Qyl.Build;

interface IQylTests : IHasSolution, IHasConfiguration
{
    [Parameter("Optional native MSTest filter for focused OTLP persistence investigations")]
    string? TelemetryTestFilter => TryGetValue(() => TelemetryTestFilter);

    Target Test => d => d
        .Description("Run every .NET test project in the solution using the completed build")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => DotNetTasks.DotNet(
            $"test --solution {Solution} --configuration {Configuration} --no-build --no-restore --max-parallel-test-modules 1",
            workingDirectory: RootDirectory));
}

/// <summary>
/// Every build, pack and gate of this repository. <c>./eng/build.sh --help</c> lists the targets;
/// <c>Check</c> is the gate before every push (<c>mise run check</c>), <c>CheckFull</c> runs everything
/// (<c>mise run check-full</c>).
/// </summary>
sealed class Build : FalloutBuild,
    ICompile,
    ICoverage,
    INativeAot,
    IVerify,
    IDependencyEdges,
    ICliContractLoop,
    IHousekeeping,
    IApiSdk,
    IApiBaseline,
    IVocabularySmoke,
    IVerifyRelease
{
    public Target VerifyTelemetryMetadata => d => d
        .Description("Verify OTLP persistence, scope identity, descriptor conflicts and missing timestamps")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => DotNetTasks.DotNet(
            $"test --project {RootDirectory / "tests" / "Qyl.Collector.Tests" / "Qyl.Collector.Tests.csproj"} --configuration {From<IHasConfiguration>().Configuration} --no-build --no-restore --max-parallel-test-modules 1 --results-directory {RootDirectory / "artifacts" / "telemetry-metadata-tests"} -- --filter {From<IQylTests>().TelemetryTestFilter ?? "FullyQualifiedName~Qyl.Collector.Tests.TelemetryMetadataTests|FullyQualifiedName~Qyl.Collector.Tests.CanonicalMetricTests|FullyQualifiedName~Qyl.Collector.Tests.OtlpRecordValidationTests"} --report-trx",
            workingDirectory: RootDirectory));

    private static string VersionLabel => GitScalar("describe --tags --always --dirty", "local");

    private static string BranchLabel => GitScalar("branch --show-current", "local");

    private static string CommitLabel => GitScalar("rev-parse --short HEAD", "N/A");


    public Target Clean => d => d
        .Before<IRestore>(static x => x.Restore)
        .Executes(() =>
        {
            var buildScriptDirectory = (RootDirectory / "eng" / "build")
                .ToString()
                .Replace('\\', '/') + "/";
            var pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            RootDirectory.GlobDirectories("**/bin", "**/obj")
                .Where(directory =>
                {
                    var path = directory.ToString().Replace('\\', '/') + "/";
                    return !path.Contains("/node_modules/", StringComparison.Ordinal)
                           // The running build, its lazy dependencies, and its assets graph live here.
                           && !path.StartsWith(buildScriptDirectory, pathComparison);
                })
                .DeleteDirectories();
            From<IHasArtifacts>().ArtifactsDirectory.CreateOrCleanDirectory();
        });

    public Target Print => d => d
        .Unlisted()
        .Before<ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  qyl Build - AI Observability Platform");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Configuration : {Configuration}", From<IHasConfiguration>().Configuration);
            Log.Information("  Version       : {Version}", VersionLabel);
            Log.Information("  Branch        : {Branch}", BranchLabel);
            Log.Information("  Commit        : {Sha}", CommitLabel);
            Log.Information("  Solution      : {Solution}", From<IHasSolution>().Solution.FileName);
            Log.Information("  IsServerBuild : {IsServer}", IsServerBuild);
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    // Trim and AOT regressions (IL2026/IL3050) are CheckFull's job: Compile builds with the AOT
    // analyzers off; PackSmoke and NativeAot are the gates that see them.
    public Target OrchestrationSmoke => d => d
        .Description("Exercise the reusable orchestrator with real .NET services, failure, cancellation and timeout")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => DotNetTasks.DotNetRun(s => s
            .SetProjectFile(From<IHasSourcePaths>().OrchestrationSmokeProject)
            .SetConfiguration(From<IHasConfiguration>().Configuration)
            .EnableNoBuild()));

    public Target Check => d => d
        .Description("The gate before every push: compile, tests, verification, the public API report, SDK and SemConv pins")
        .DependsOn<ICompile>(static x => x.Compile)
        .DependsOn<IQylTests>(static x => x.Test)
        .DependsOn(OrchestrationSmoke)
        .DependsOn<IVerify>(static x => x.Verify)
        .DependsOn<IApiBaseline>(static x => x.ApiBaseline)
        .DependsOn<IHousekeeping>(static x => x.VerifySdkVersions)
        .DependsOn<IHousekeeping>(static x => x.VerifySemanticConventionsPin);

    public Target CheckFull => d => d
        .Description("Everything: Check, NativeAOT, the pack smoke, the SDK proof, the vocabulary smoke and the coverage gate")
        .DependsOn(Check)
        .DependsOn<INativeAot>(static x => x.NativeAot)
        // The pack lane builds with the AOT analyzers Compile disables, so it is the only gate that
        // sees IL2026/IL3050. Omitting it from the full gate is what let a trim regression reach
        // main past a green local run.
        .DependsOn<IPack>(static x => x.PackSmoke)
        // The SDK's own nine-stage proof: it ships from here, so its gates are this gate.
        .DependsOn<IApiSdk>(static x => x.ApiSdk)
        .DependsOn<IVocabularySmoke>(static x => x.G1VocabularySmoke)
        .DependsOn<ICoverage>(static x => x.Coverage);

    protected override void OnTargetSkipped(string target)
    {
        if (IsServerBuild && InvokedTargets.Any(static target => target.Name == nameof(CheckFull)))
            throw new InvalidOperationException($"CheckFull requires every stage in CI; {target} was skipped.");
    }

    AbsolutePath IHasArtifacts.ArtifactsDirectory => RootDirectory / "artifacts";

    Configure<DotNetBuildSettings> ICompile.CompileSettings => s => s
        .SetDeterministic(IsServerBuild)
        .SetContinuousIntegrationBuild(IsServerBuild)
        .SetProperty("QylAot", false);

    T From<T>() where T : IFalloutBuild => (T)(object)this;

    private static string GitScalar(string args, string fallback)
    {
        try
        {
            return GitTasks.Git(args, RootDirectory, logOutput: false, logInvocation: false)
                       .Select(static output => output.Text.Trim())
                       .FirstOrDefault(static text => text.Length > 0)
                   ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static int Main() => Execute<Build>(static x => ((ICompile)x).Compile);
}
