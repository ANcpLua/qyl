
using System;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Components;

namespace Qyl.Build;


interface IHasSourcePaths : IHasSolution, IHasArtifacts
{
    new AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";
    AbsolutePath ServicesDirectory => RootDirectory / "services";
    AbsolutePath PackagesDirectory => RootDirectory / "packages";
    AbsolutePath InternalDirectory => RootDirectory / "internal";
    AbsolutePath CollectorDirectory => ServicesDirectory / "qyl.collector";
    AbsolutePath DashboardDirectory => ServicesDirectory / "qyl.dashboard";
    AbsolutePath QylToolSmokeProject => RootDirectory / "eng" / "tools" / "QylToolSmoke" / "QylToolSmoke.csproj";
    AbsolutePath ComposeFile => RootDirectory / "eng" / "compose.yaml";

    /// <summary>Projects with IsPackable=true — the packages qyl actually ships.</summary>
    AbsolutePath[] ShippablePackProjects =>
    [
        PackagesDirectory / "Qyl.Cli" / "Qyl.Cli.csproj",
        PackagesDirectory / "Qyl.Api.Sdk" / "Qyl.Api.Sdk.csproj"
    ];

    string[] ShippablePackageIds =>
    [
        "qyl",
        "qyl.linux-x64",
        "qyl.linux-arm64",
        "qyl.osx-x64",
        "qyl.osx-arm64",
        "qyl.win-x64",
        "qyl.win-arm64",
        "Qyl.Api.Sdk"
    ];
}

interface IVersionize : IHasSourcePaths
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
