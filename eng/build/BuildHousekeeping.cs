// Targets adapted from open-telemetry/opentelemetry-dotnet-instrumentation (Apache-2.0).

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

namespace Qyl.Build;

[ParameterPrefix(nameof(IHousekeeping))]
interface IHousekeeping : IHazSourcePaths
{
    AbsolutePath ToolsDirectory => RootDirectory / "eng" / "tools";

    [Parameter("Requested .NET 10 SDK version for UpdateSdkVersions (e.g. 10.0.302)")]
    string? SdkVersion => TryGetValue(() => SdkVersion);

    Target VerifySdkVersions => d => d
        .Description("Verify pinned .NET SDK versions in workflows and dockerfiles match global.json")
        .Executes(() =>
            DotNetTasks.DotNet(
                $"run --project \"{ToolsDirectory / "SdkVersionAnalyzer"}\" -- --verify \"{RootDirectory}\"",
                workingDirectory: RootDirectory));

    Target UpdateSdkVersions => d => d
        .Description("Rewrite pinned .NET SDK versions in workflows and dockerfiles (pass --housekeeping-sdk-version)")
        .Requires(() => SdkVersion)
        .Executes(() =>
            DotNetTasks.DotNet(
                $"run --project \"{ToolsDirectory / "SdkVersionAnalyzer"}\" -- --modify \"{RootDirectory}\" - - {SdkVersion}",
                workingDirectory: RootDirectory));

    Target GenerateLibraryVersions => d => d
        .Unlisted()
        .Description("Generate the package version matrix sources into Artifacts/generated (opt-in scaffolding)")
        .Executes(() =>
            DotNetTasks.DotNet(
                $"run --project \"{ToolsDirectory / "LibraryVersionsGenerator"}\"",
                workingDirectory: RootDirectory));
}
