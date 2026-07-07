
using System;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Components;

namespace Qyl.Build;


interface IHazSourcePaths : IHazSolution, IHazArtifacts
{
    new AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";
    AbsolutePath ServicesDirectory => RootDirectory / "services";
    AbsolutePath PackagesDirectory => RootDirectory / "packages";
    AbsolutePath InternalDirectory => RootDirectory / "internal";
    AbsolutePath CollectorDirectory => ServicesDirectory / "qyl.collector";
    AbsolutePath DashboardDirectory => ServicesDirectory / "qyl.dashboard";
    AbsolutePath ComposeFile => RootDirectory / "eng" / "compose.yaml";

    /// <summary>
    ///     Projects with IsPackable=true — the packages qyl actually ships.
    ///     Qyl.Run.Host is deliberately not here: its csproj keeps the runnable
    ///     launcher unpacked and Qyl.Run as the packable artifact.
    /// </summary>
    AbsolutePath[] ShippablePackProjects =>
    [
        PackagesDirectory / "Qyl.Run" / "Qyl.Run.csproj"
    ];
}

interface IVersionize : IHazSourcePaths
{
    [PathVariable]
    Tool Versionize => TryGetValue(() => Versionize)
                       ?? throw new InvalidOperationException(
                           "Versionize tool not found. Install: dotnet tool install -g Versionize");

    Target Changelog => d => d
        .Unlisted()
        .Description("Generate CHANGELOG from conventional commits (Release runs this)")
        .Executes(() => Versionize("--dry-run", RootDirectory));

    Target Release => d => d
        .Description("Bump version, update CHANGELOG, create tag")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() => Versionize(null, RootDirectory));
}
