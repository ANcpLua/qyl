using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Qyl.Build;

/// <summary>
/// G7/G11: the actual qyl package graph equals the architecture's §2 edge list exactly,
/// for every project in the repository, <c>internal/</c> included. Anything not listed is
/// forbidden — a new edge is a deliberate table change reviewed against the architecture,
/// never a quiet csproj addition. The table speaks the published Qyl.Telemetry.* identities,
/// rewritten in the same commits as the pin bumps that consumed them.
/// </summary>
interface IDependencyEdges : IHazSourcePaths
{
    /// <summary>qyl-family package references each project may carry, exhaustively.</summary>
    private static Dictionary<string, string[]> AllowedQylPackageEdges => new(StringComparer.Ordinal)
    {
        // Catalog generation input only (loop 1): the build reflects over the pinned
        // semconv packages to generate the collector ingest catalog.
        ["eng/build/build.csproj"] =
            ["Qyl.Telemetry.SemanticConventions", "Qyl.Telemetry.SemanticConventions.Incubating"],
        // Conformance/smoke tooling exercises the published contracts and producer stack.
        ["eng/tools/QylSdkConformance/QylSdkConformance.csproj"] = ["Qyl.Api.Contracts"],
        // Collector-defaults layer: consumes the published composition (self-telemetry via
        // AddQyl) plus the shared vocabulary; never a private copy of the producer pipeline.
        ["internal/qyl.instrumentation/qyl.instrumentation.csproj"] =
        [
            "Qyl.Telemetry.Hosting", "Qyl.Api.Contracts",
            "Qyl.Telemetry.SemanticConventions", "Qyl.Telemetry.SemanticConventions.Incubating",
        ],
        // The Qyl.Api.Sdk runtime publishes one registry name of its own, the resource attribute
        // qyl.api.contract.revision, and takes it from the vocabulary rather than spelling it: the
        // collector's ingest policy reads the same registry, and a literal on either side would be a
        // second source of truth. Compile-time only (a const, PrivateAssets=all); the producer family
        // reaches a Qyl API through the SDK's Qyl.Telemetry.Hosting reference, not through here.
        ["packages/Qyl.Api.Sdk/Qyl.Api/Qyl.Api.csproj"] = ["Qyl.Telemetry.SemanticConventions.Incubating"],
        // G11: the CLI is a client of the collector API and owns none of it.
        ["packages/Qyl.Cli/Qyl.Cli.csproj"] = ["Qyl.Api.Contracts"],
        // The demo producer sets attributes through the pre-generated Activities classes;
        // the retired compile-time generator package emitted them into the assembly instead.
        ["packages/Qyl.Run.Workload/Qyl.Run.Workload.csproj"] =
            ["Qyl.Telemetry.SemanticConventions", "Qyl.Telemetry.SemanticConventions.Incubating"],
        // Collector product function: generated contracts it serves, plus the vocabulary it
        // normalizes ingested attributes against (the generated mapping table and the metric
        // definitions). The producer stack arrives only transitively through the
        // collector-defaults layer (self-telemetry); a direct producer-family reference here
        // is the forbidden edge G7 exists to catch.
        ["services/qyl.collector/qyl.collector.csproj"] =
        [
            "Qyl.Api.Contracts",
            "Qyl.Telemetry.SemanticConventions", "Qyl.Telemetry.SemanticConventions.Incubating",
        ],
        ["tests/Qyl.Sdk.Conformance/Qyl.Sdk.Conformance.csproj"] = ["Qyl.Telemetry.Hosting"],
        // Every project built by the Qyl.Api.Sdk MSBuild SDK, which injects these from its own targets: the
        // producer's onboarding surface and the package its interceptor generator ships in. A project moves
        // into this row by naming that SDK, so the row is also what makes the move visible — a client-ring
        // project could otherwise acquire the whole producer family by changing one attribute.
        [QylApiSdkConsumer] = ["Qyl.Telemetry.AutoInstrumentation", "Qyl.Telemetry.Hosting"],
    };

    /// <summary>The §2 row every project built by the Qyl.Api.Sdk MSBuild SDK is held to.</summary>
    private const string QylApiSdkConsumer = "<Sdk=\"Qyl.Api.Sdk\">";

    /// <summary>The MSBuild files the SDK injects its package references from, read as if they were the consumer's.</summary>
    private static readonly string[] s_qylApiSdkBuildFiles =
    [
        "packages/Qyl.Api.Sdk/Build/Qyl.Sdk.Api.props",
        "packages/Qyl.Api.Sdk/Build/Qyl.Sdk.Api.targets",
    ];

    /// <summary>
    /// G11 on the project axis: the collector is reachable only via its API, so no project
    /// may take a compile-time <c>ProjectReference</c> on it. Only the collector's own test
    /// project is exempt — it hosts the service in-process to drive it. Without this, a
    /// client-ring project could reach the collector's internals while its
    /// <c>PackageReference</c> list still equalled the §2 table exactly and the package-axis
    /// assertion below stayed green.
    /// </summary>
    private static readonly string[] s_collectorProjectReferenceExemptions =
    [
        "tests/Qyl.Collector.Tests/Qyl.Collector.Tests.csproj",
    ];

    /// <summary>
    /// Packages forbidden anywhere in the repository. Every entry bans the exact ID and all of
    /// its sub-packages: "Microsoft.Extensions.AI" also catches
    /// Microsoft.Extensions.AI.Abstractions, the ID most consumers actually reference.
    /// </summary>
    private static readonly string[] s_forbiddenEverywhere =
    [
        // A telemetry sink does not embed an agent runtime; self-telemetry never justifies one.
        "ANcpLua.Agents",
        "Microsoft.Extensions.AI",
        "Microsoft.Agents",
        // The collector is a process reached via its API, never a package.
        "Qyl.Collector",
    ];

    private static bool IsForbidden(string reference, string forbidden) =>
        reference.Equals(forbidden, StringComparison.OrdinalIgnoreCase)
        || reference.StartsWith(forbidden + ".", StringComparison.OrdinalIgnoreCase);


    /// <summary>
    /// Whether a project is built by the Qyl.Api.Sdk, by either route it offers: <c>Sdk="Qyl.Api.Sdk/x"</c> on
    /// the project, or the explicit <c>Sdk/Sdk.props</c> and <c>Sdk/Sdk.targets</c> imports the sample uses so a
    /// fresh clone builds without a pack step.
    /// </summary>
    private static bool UsesQylApiSdk(XDocument document) =>
        ((string?)document.Root?.Attribute("Sdk"))?.StartsWith("Qyl.Api.Sdk", StringComparison.OrdinalIgnoreCase) is true ||
        document.Descendants("Import")
            .Select(static import => (string?)import.Attribute("Project"))
            .Any(static project => project?.Replace('\\', '/')
                .Contains("Qyl.Api.Sdk/Sdk/Sdk.", StringComparison.OrdinalIgnoreCase) is true);

    Target VerifyDependencyEdges => d => d
        .Unlisted()
        .Executes(() =>
        {
            var repoRoot = NukeBuild.RootDirectory;
            var offenders = new List<string>();
            var seenProjects = new HashSet<string>(StringComparer.Ordinal);

            var projects = repoRoot.GlobFiles("services/**/*.csproj", "internal/**/*.csproj",
                    "packages/**/*.csproj", "samples/**/*.csproj", "tests/**/*.csproj", "eng/**/*.csproj")
                .Where(static p => !p.ToString().Contains("/node_modules/", StringComparison.Ordinal)
                                   && !p.ToString().Contains("/Artifacts/", StringComparison.Ordinal)
                                   && !p.ToString().Contains("/artifacts/", StringComparison.Ordinal));

            // PackageReference/GlobalPackageReference items in shared MSBuild files reach every
            // project beneath them at evaluation time; reading only the csproj XML would let a
            // forbidden edge hide in a Directory.Build.props (a live pattern in this repo —
            // packages/Directory.Build.props already contributes one analyzer reference).
            var sharedReferences = repoRoot
                .GlobFiles("**/Directory.Build.props", "**/Directory.Build.targets", "**/Directory.Packages.props")
                .Where(static p => !p.ToString().Contains("/node_modules/", StringComparison.Ordinal)
                                   && !p.ToString().Contains("/Artifacts/", StringComparison.Ordinal)
                                   && !p.ToString().Contains("/artifacts/", StringComparison.Ordinal))
                .Select(static file => (
                    Directory: file.Parent.ToString().Replace('\\', '/'),
                    References: XDocument.Load(file).Descendants()
                        .Where(static e => e.Name.LocalName is "PackageReference" or "GlobalPackageReference")
                        .Select(static r => (string?)r.Attribute("Include"))
                        .Where(static include => include is not null)
                        .Select(static include => include!)
                        .ToList()))
                .Where(static entry => entry.References.Count > 0)
                .ToList();

            // The Qyl.Api.Sdk injects PackageReferences from its own Build/*.props|targets, which no consumer's
            // csproj mentions. Read once here and attributed to every project that names the SDK, so a project
            // built by it is measured against what it actually gets.
            var sdkInjectedReferences = s_qylApiSdkBuildFiles
                .Select(file => repoRoot / file)
                .Where(static file => file.FileExists())
                .SelectMany(static file => XDocument.Load(file).Descendants()
                    .Where(static e => e.Name.LocalName is "PackageReference")
                    .Select(static r => (string?)r.Attribute("Include"))
                    .Where(static include => include is not null)
                    .Select(static include => include!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var project in projects)
            {
                var relative = repoRoot.GetRelativePathTo(project).ToString().Replace('\\', '/');
                seenProjects.Add(relative);
                var document = XDocument.Load(project);
                var usesQylApiSdk = UsesQylApiSdk(document);
                var projectPath = project.ToString().Replace('\\', '/');
                var references = document.Descendants("PackageReference")
                    .Select(static r => (string?)r.Attribute("Include"))
                    .Where(static include => include is not null)
                    .Select(static include => include!)
                    .Concat(sharedReferences
                        .Where(entry => projectPath.StartsWith(entry.Directory + "/", StringComparison.Ordinal))
                        .SelectMany(static entry => entry.References))
                    .Concat(usesQylApiSdk ? sdkInjectedReferences : [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!s_collectorProjectReferenceExemptions.Contains(relative, StringComparer.Ordinal))
                {
                    var collectorEdges = document.Descendants("ProjectReference")
                        .Select(static r => (string?)r.Attribute("Include"))
                        .Where(static include => include is not null)
                        .Select(static include => include!.Replace('\\', '/'))
                        .Where(static include => include.EndsWith(
                            "services/qyl.collector/qyl.collector.csproj", StringComparison.Ordinal));

                    offenders.AddRange(collectorEdges.Select(edge =>
                        $"{relative}: ProjectReference on the collector ({edge}) — G11: the " +
                        "collector is reachable only via its API"));
                }

                foreach (var reference in references)
                {
                    if (s_forbiddenEverywhere.Any(forbidden => IsForbidden(reference, forbidden)))
                        offenders.Add($"{relative}: forbidden package {reference}");
                }

                var qylReferences = references
                    .Where(static r => r.StartsWith("Qyl", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(static r => r, StringComparer.Ordinal)
                    .ToArray();
                var row = usesQylApiSdk ? QylApiSdkConsumer : relative;
                seenProjects.Add(row);
                var allowed = AllowedQylPackageEdges.TryGetValue(row, out var edges)
                    ? edges.OrderBy(static e => e, StringComparer.Ordinal).ToArray()
                    : [];

                if (!qylReferences.SequenceEqual(allowed, StringComparer.Ordinal))
                {
                    offenders.Add(
                        $"{relative} (row {row}): qyl package edges [{string.Join(", ", qylReferences)}] " +
                        $"do not equal the §2 table [{string.Join(", ", allowed)}]");
                }
            }

            var staleRows = AllowedQylPackageEdges.Keys.Where(key => !seenProjects.Contains(key)).ToList();
            offenders.AddRange(staleRows.Select(static row => $"edge table names a missing project: {row}"));

            if (offenders.Count > 0)
            {
                throw new InvalidOperationException(
                    "Dependency edges diverge from the architecture §2 edge list:" + Environment.NewLine +
                    string.Join(Environment.NewLine, offenders));
            }

            Log.Information("Dependency edges match the §2 table across {Count} projects", seenProjects.Count);
        });
}
