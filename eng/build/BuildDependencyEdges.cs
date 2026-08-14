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
        // G11: the CLI is a client of the collector API. Its observer also emits qyl-owned
        // telemetry using the published incubating vocabulary rather than local string copies.
        ["packages/Qyl.Cli/Qyl.Cli.csproj"] =
            ["Qyl.Api.Contracts", "Qyl.Telemetry.SemanticConventions.Incubating"],
        ["packages/Qyl.Run.Workload/Qyl.Run.Workload.csproj"] =
            ["Qyl.Telemetry.SemanticConventions.SourceGeneration"],
        // Collector product function: generated contracts it serves. The producer stack
        // arrives only transitively through the collector-defaults layer (self-telemetry);
        // a direct producer-family reference here is the forbidden edge G7 exists to catch.
        ["services/qyl.collector/qyl.collector.csproj"] = ["Qyl.Api.Contracts"],
        ["tests/Qyl.Sdk.Conformance/Qyl.Sdk.Conformance.csproj"] = ["Qyl.Telemetry.Hosting"],
    };

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

    Target VerifyDependencyEdges => d => d
        .Unlisted()
        .Executes(() =>
        {
            var repoRoot = NukeBuild.RootDirectory;
            var offenders = new List<string>();
            var seenProjects = new HashSet<string>(StringComparer.Ordinal);

            var projects = repoRoot.GlobFiles("services/**/*.csproj", "internal/**/*.csproj",
                    "packages/**/*.csproj", "tests/**/*.csproj", "eng/**/*.csproj")
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

            foreach (var project in projects)
            {
                var relative = repoRoot.GetRelativePathTo(project).ToString().Replace('\\', '/');
                seenProjects.Add(relative);
                var document = XDocument.Load(project);
                var projectPath = project.ToString().Replace('\\', '/');
                var references = document.Descendants("PackageReference")
                    .Select(static r => (string?)r.Attribute("Include"))
                    .Where(static include => include is not null)
                    .Select(static include => include!)
                    .Concat(sharedReferences
                        .Where(entry => projectPath.StartsWith(entry.Directory + "/", StringComparison.Ordinal))
                        .SelectMany(static entry => entry.References))
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
                var allowed = AllowedQylPackageEdges.TryGetValue(relative, out var edges)
                    ? edges.OrderBy(static e => e, StringComparer.Ordinal).ToArray()
                    : [];

                if (!qylReferences.SequenceEqual(allowed, StringComparer.Ordinal))
                {
                    offenders.Add(
                        $"{relative}: qyl package edges [{string.Join(", ", qylReferences)}] " +
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
