// Pack pipeline adapted from open-telemetry/opentelemetry-dotnet-instrumentation's
// Build.NuGet.Steps.cs (Apache-2.0): pack shippables into one artifacts folder and
// purge stale copies of the same ids from the local NuGet cache so a freshly packed
// version is always the one restored.

using System;
using System.IO;
using System.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Fallout.Components;
using Serilog;

namespace Qyl.Build;

interface IPack : IHasSourcePaths, IHasConfiguration
{
    AbsolutePath NuGetArtifactsDirectory => ArtifactsDirectory / "nuget";

    /// <summary>Where <c>PackPlatform</c> packs the host runtime's <c>qyl.&lt;rid&gt;</c> package.</summary>
    AbsolutePath PlatformPackagesDirectory => ArtifactsDirectory / "qyl-packages";

    [Parameter("Package version PackPlatform and PackRelease stamp (release.sh passes Version.props' QylVersion)")]
    string? PackageVersion => TryGetValue(() => PackageVersion);

    [Parameter("Commit PackPlatform and PackRelease stamp as RepositoryCommit and SourceRevisionId")]
    string? RepositoryCommit => TryGetValue(() => RepositoryCommit);

    [Parameter("Runtime identifier PackPlatform packs the qyl tool for; it must be the one the host dotnet reports")]
    string? ToolRuntimeIdentifier => TryGetValue(() => ToolRuntimeIdentifier);

    /// <summary>
    /// What both release packs share. Neither builds the dashboard nor compiles the solution first:
    /// the embedded dashboard is built beforehand (FrontendBuild, in Check), and each pack builds its project.
    /// </summary>
    private Configure<DotNetPackSettings> ReleasePackSettings => s => s
        .SetConfiguration(Configuration)
        .SetVersion(PackageVersion)
        .SetProperty("RepositoryCommit", RepositoryCommit!)
        .SetProperty("SourceRevisionId", RepositoryCommit!);

    Target PackPlatform => d => d
        .Description("Pack the qyl tool for the host runtime into artifacts/qyl-packages (one platform of the release set)")
        .Requires(() => PackageVersion, () => RepositoryCommit, () => ToolRuntimeIdentifier)
        .Executes(() =>
        {
            // The first ` RID:` line of `dotnet --info`: a host whose dotnet reports another runtime
            // (an x64 SDK on an arm64 host) would pack the wrong package.
            var hostRuntime = DotNetTasks.DotNet("--info", RootDirectory, logOutput: false)
                .Select(static line => line.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .Where(static fields => fields.Length > 1 && fields[0] == "RID:")
                .Select(static fields => fields[1])
                .FirstOrDefault();
            if (hostRuntime != ToolRuntimeIdentifier)
                throw new InvalidOperationException($"Runner resolved RID '{hostRuntime}', expected '{ToolRuntimeIdentifier}'.");

            PlatformPackagesDirectory.CreateDirectory();
            DotNetTasks.DotNetPack(s => s
                .Apply(ReleasePackSettings)
                .SetProject(CliPackProject)
                .SetOutputDirectory(PlatformPackagesDirectory)
                .SetProperty("ToolPackageRuntimeIdentifiers", ToolRuntimeIdentifier!));
        });

    Target PackRelease => d => d
        .Description("Pack the versioned release set (qyl, Qyl.Api.Sdk, Qyl.Orchestration) into a clean artifacts/nuget (release.sh's pack step)")
        .Requires(() => PackageVersion, () => RepositoryCommit)
        .Executes(() =>
        {
            NuGetArtifactsDirectory.CreateOrCleanDirectory();
            DotNetTasks.DotNetPack(s => s
                .Apply(ReleasePackSettings)
                .SetOutputDirectory(NuGetArtifactsDirectory)
                .CombineWith(ShippablePackProjects, static (settings, project) => settings
                    .SetProject(project)));
        });

    Target Pack => d => d
        .Description("Pack the shippable packages into artifacts/nuget")
        .DependsOn<IPipeline>(static x => x.FrontendBuild)
        .DependsOn<ICompile>(static x => x.Compile)
        .Produces(NuGetArtifactsDirectory / "*.nupkg")
        .Executes(() =>
        {
            NuGetArtifactsDirectory.CreateOrCleanDirectory();

            DotNetTasks.DotNetPack(s => s
                .SetConfiguration(Configuration)
                .SetOutputDirectory(NuGetArtifactsDirectory)
                .CombineWith(ShippablePackProjects, static (settings, project) => settings
                    .SetProject(project)));

            foreach (var package in NuGetArtifactsDirectory.GlobFiles("*.nupkg"))
            {
                Log.Information("Packed: {Package}", package.Name);
            }
        });

    Target PackSmoke => d => d
        .Description("Install the packed qyl tool into a clean directory and exercise its product vertical")
        .DependsOn(Pack)
        .Executes(() => DotNetTasks.DotNetRun(s => s
            .SetProjectFile(QylToolSmokeProject)
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .SetApplicationArguments(NuGetArtifactsDirectory)));

    Target CleanLocalPackagesCache => d => d
        .Unlisted()
        .Description("Remove locally packed qyl package ids from the global NuGet cache so fresh packs win")
        .Executes(() =>
        {
            // Background builds can hold cached package files; shutting down build
            // servers reduces the chance of a locked cache directory.
            DotNetTasks.DotNet("build-server shutdown");

            var output = DotNetTasks.DotNet("nuget locals global-packages --list", RootDirectory, logOutput: false);
            foreach (var line in output.Where(static line => line.Text.StartsWith("global-packages: ", StringComparison.Ordinal)))
            {
                AbsolutePath packagesDir = Path.GetFullPath(line.Text["global-packages: ".Length..].Trim());
                foreach (var packageId in ShippablePackageIds)
                {
                    var cachedPackage = packagesDir / packageId;
                    if (cachedPackage.DirectoryExists())
                    {
                        cachedPackage.DeleteDirectory();
                        Log.Information("Purged cached package: {Package}", cachedPackage);
                    }
                }
            }
        });
}
