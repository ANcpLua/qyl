// Targets adapted from open-telemetry/opentelemetry-dotnet-instrumentation (Apache-2.0).

using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;

namespace Qyl.Build;

[ParameterPrefix(nameof(IHousekeeping))]
interface IHousekeeping : IHasSourcePaths
{
    AbsolutePath ToolsDirectory => RootDirectory / "eng" / "tools";

    [Parameter("Requested .NET 10 SDK version for UpdateSdkVersions (e.g. 10.0.400)")]
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
}
