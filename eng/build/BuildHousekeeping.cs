// Targets adapted from open-telemetry/opentelemetry-dotnet-instrumentation (Apache-2.0).

using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Fallout.Components;
using NuGet.Versioning;
using Serilog;

namespace Qyl.Build;

[ParameterPrefix(nameof(IHousekeeping))]
interface IHousekeeping : IHasSourcePaths, IHasConfiguration
{
    AbsolutePath ToolsDirectory => RootDirectory / "eng" / "tools";

    [Parameter("Requested .NET 10 SDK version for UpdateSdkVersions (e.g. 10.0.400)")]
    string? SdkVersion => TryGetValue(() => SdkVersion);

    Target VerifySdkVersions => d => d
        .ProceedAfterFailure()
        .Description("Verify pinned .NET SDK versions in dockerfiles (and any workflow files) match global.json")
        .Executes(() =>
            DotNetTasks.DotNet(
                $"run --project \"{ToolsDirectory / "SdkVersionAnalyzer"}\" --configuration {Configuration} --no-build -- --verify \"{RootDirectory}\"",
                workingDirectory: RootDirectory));

    /// <summary>
    /// qyl pins Qyl.Telemetry.AutoInstrumentation (QylTelemetryVersion) and Qyl.Telemetry.SemanticConventions
    /// (QylSemanticConventionsVersion) independently, and the conventions are C# const strings: a caller compiled
    /// against one SemConv release keeps emitting that release's vocabulary whatever this repository restores
    /// beside it. The restored AutoInstrumentation package therefore has to be compiled against the pinned
    /// SemConv version. Its assembly says so in AssemblyMetadata("QylSemanticConventionsVersion", ...) (from
    /// the release that introduces it); a package without the attribute is judged by its nuspec's dependency
    /// lower bound on Qyl.Telemetry.SemanticConventions, which is the version it was built with.
    /// </summary>
    Target VerifySemanticConventionsPin => d => d
        .ProceedAfterFailure()
        .Description("Verify the restored Qyl.Telemetry.AutoInstrumentation package was compiled against the pinned Qyl.Telemetry.SemanticConventions version")
        .After<IRestore>(static x => x.Restore)
        .Executes(() =>
        {
            var versionProps = XDocument.Load(RootDirectory / "Version.props");
            var telemetryVersion = VersionProperty(versionProps, "QylTelemetryVersion");
            var pinnedSemConv = NuGetVersion.Parse(VersionProperty(versionProps, "QylSemanticConventionsVersion"));

            var packageDirectory = NuGetPackagesDirectory / "qyl.telemetry.autoinstrumentation" / telemetryVersion;
            if (!packageDirectory.DirectoryExists())
                throw new InvalidOperationException(
                    $"Qyl.Telemetry.AutoInstrumentation {telemetryVersion} is not restored at {packageDirectory}; restore the solution first.");

            var (compiledAgainst, source) = CompiledSemanticConventionsVersion(packageDirectory);
            if (compiledAgainst != pinnedSemConv)
            {
                throw new InvalidOperationException(
                    $"Qyl.Telemetry.AutoInstrumentation {telemetryVersion} was compiled against Qyl.Telemetry.SemanticConventions " +
                    $"{compiledAgainst} ({source}), but Version.props pins QylSemanticConventionsVersion {pinnedSemConv}. " +
                    "Its const strings carry the older vocabulary into every caller: move QylTelemetryVersion to a release " +
                    "built on the pinned SemConv, or pin the SemConv version it was built with.");
            }

            Log.Information(
                "Qyl.Telemetry.AutoInstrumentation {Telemetry} is compiled against Qyl.Telemetry.SemanticConventions {SemConv} ({Source})",
                telemetryVersion, compiledAgainst, source);
        });

    private static AbsolutePath NuGetPackagesDirectory =>
        Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } packages
            ? AbsolutePath.Create(packages)
            : AbsolutePath.Create(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) / ".nuget" / "packages";

    private static string VersionProperty(XDocument versionProps, string name) =>
        versionProps.Descendants(name).Select(static element => element.Value.Trim()).SingleOrDefault(static value => value.Length > 0)
        ?? throw new InvalidOperationException($"Version.props must define one {name}.");

    private static (NuGetVersion Version, string Source) CompiledSemanticConventionsVersion(AbsolutePath packageDirectory)
    {
        foreach (var assembly in (packageDirectory / "lib").GlobFiles("**/*.dll").OrderBy(static path => path.ToString(), StringComparer.Ordinal))
        {
            if (AssemblyMetadataValue(assembly, "QylSemanticConventionsVersion") is { } declared)
                return (NuGetVersion.Parse(declared), $"AssemblyMetadata in {assembly.Name}");
        }

        var nuspec = packageDirectory.GlobFiles("*.nuspec").SingleOrDefault()
                     ?? throw new InvalidOperationException($"No nuspec in {packageDirectory}.");
        var range = XDocument.Load(nuspec).Descendants()
            .Where(static element => element.Name.LocalName == "dependency")
            .Where(static element => string.Equals(element.Attribute("id")?.Value, "Qyl.Telemetry.SemanticConventions", StringComparison.OrdinalIgnoreCase))
            .Select(static element => element.Attribute("version")?.Value)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
            ?? throw new InvalidOperationException($"{nuspec.Name} declares no dependency on Qyl.Telemetry.SemanticConventions.");
        var lowerBound = VersionRange.Parse(range).MinVersion
                         ?? throw new InvalidOperationException($"{nuspec.Name} gives Qyl.Telemetry.SemanticConventions no lower bound ({range}).");
        return (lowerBound, $"nuspec dependency lower bound {range}; the assembly carries no QylSemanticConventionsVersion metadata");
    }

    /// <summary>Reads one <c>[assembly: AssemblyMetadata(key, value)]</c> from the file's metadata; the assembly is never loaded.</summary>
    private static string? AssemblyMetadataValue(AbsolutePath assemblyPath, string key)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
            return null;
        var metadata = peReader.GetMetadataReader();
        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (AttributeTypeName(metadata, attribute.Constructor) != "AssemblyMetadataAttribute")
                continue;
            var blob = metadata.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 0x0001)
                continue;
            var attributeKey = blob.ReadSerializedString();
            var attributeValue = blob.ReadSerializedString();
            if (string.Equals(attributeKey, key, StringComparison.Ordinal))
                return attributeValue;
        }

        return null;
    }

    private static string? AttributeTypeName(MetadataReader metadata, EntityHandle constructor) =>
        constructor.Kind switch
        {
            HandleKind.MemberReference when metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent is { Kind: HandleKind.TypeReference } parent =>
                metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)parent).Name),
            HandleKind.MethodDefinition =>
                metadata.GetString(metadata.GetTypeDefinition(metadata.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType()).Name),
            _ => null
        };

    Target UpdateSdkVersions => d => d
        .Description("Rewrite pinned .NET SDK versions in dockerfiles (and any workflow files) (pass --housekeeping-sdk-version)")
        .Requires(() => SdkVersion)
        .Executes(() =>
            DotNetTasks.DotNet(
                $"run --project \"{ToolsDirectory / "SdkVersionAnalyzer"}\" -- --modify \"{RootDirectory}\" - - {SdkVersion}",
                workingDirectory: RootDirectory));
}
