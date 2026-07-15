// Pack pipeline adapted from open-telemetry/opentelemetry-dotnet-instrumentation's
// Build.NuGet.Steps.cs (Apache-2.0): pack shippables into one artifacts folder and
// purge stale copies of the same ids from the local NuGet cache so a freshly packed
// version is always the one restored.

using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Components;
using Serilog;

namespace Qyl.Build;

interface IPack : IHazSourcePaths, IHazConfiguration
{
    AbsolutePath NuGetArtifactsDirectory => ArtifactsDirectory / "nuget";

    Target Pack => d => d
        .Description("Pack the shippable packages into Artifacts/nuget")
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
