using Fallout.Common.IO;
using Fallout.Components;

namespace Qyl.Build;


interface IHasSourcePaths : IHasSolution, IHasArtifacts
{
    new AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath ServicesDirectory => RootDirectory / "services";
    AbsolutePath PackagesDirectory => RootDirectory / "packages";
    AbsolutePath InternalDirectory => RootDirectory / "internal";
    AbsolutePath CollectorDirectory => ServicesDirectory / "qyl.collector";
    AbsolutePath CollectorIngestionDirectory => ServicesDirectory / "qyl.collector.ingestion";
    AbsolutePath CollectorStorageDirectory => ServicesDirectory / "qyl.collector.storage";

    /// <summary>The collector's three projects. A check about "the collector" scans all of them.</summary>
    AbsolutePath[] CollectorProjectDirectories => [CollectorDirectory, CollectorIngestionDirectory, CollectorStorageDirectory];
    AbsolutePath QylToolSmokeProject => RootDirectory / "eng" / "tools" / "QylToolSmoke" / "QylToolSmoke.csproj";

    /// <summary>The qyl tool: the outer <c>qyl</c> package and, packed per runtime, its <c>qyl.&lt;rid&gt;</c> packages.</summary>
    AbsolutePath CliPackProject => PackagesDirectory / "Qyl.Cli" / "Qyl.Cli.csproj";

    AbsolutePath OrchestrationProject => PackagesDirectory / "Qyl.Orchestration" / "Qyl.Orchestration.csproj";
    AbsolutePath OrchestrationSmokeProject => RootDirectory / "eng" / "tools" / "QylOrchestrationSmoke" / "QylOrchestrationSmoke.csproj";

    /// <summary>Projects with IsPackable=true — the packages qyl actually ships.</summary>
    AbsolutePath[] ShippablePackProjects =>
    [
        CliPackProject,
        OrchestrationProject,
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
        "Qyl.Api.Sdk",
        "Qyl.Orchestration"
    ];
}
