// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
// qyl adaptation: the upstream file enumerated every instrumented library of the
// OpenTelemetry .NET auto-instrumentation (Azure, EF Core, GraphQL, NLog, ...).
// qyl's matrix starts from the packages its collector and instrumentation actually
// consume; "*" resolves to the version pinned in Directory.Packages.props.

using LibraryVersionsGenerator.Models;

namespace LibraryVersionsGenerator;

internal static class PackageVersionDefinitions
{
    public static IReadOnlyCollection<PackageVersionDefinition> Definitions =>
    [
        new()
        {
            IntegrationName = "OpenTelemetrySdk",
            NugetPackageName = "OpenTelemetry.Exporter.OpenTelemetryProtocol",
            TestApplicationName = "TestApplication.OpenTelemetrySdk",
            Versions =
            [
                new("*")
            ]
        },
        new()
        {
            IntegrationName = "MicrosoftExtensionsAI",
            NugetPackageName = "Microsoft.Extensions.AI",
            TestApplicationName = "TestApplication.MicrosoftExtensionsAI",
            Versions =
            [
                new("*")
            ]
        },
        new()
        {
            IntegrationName = "DuckDB",
            NugetPackageName = "DuckDB.NET.Data.Full",
            TestApplicationName = "TestApplication.DuckDB",
            Versions =
            [
                new("*")
            ]
        }
    ];

    internal sealed record PackageVersionDefinition
    {
        public required string IntegrationName { get; init; }

        public required string NugetPackageName { get; init; }

        public required string TestApplicationName { get; init; }

        public required IReadOnlyCollection<PackageVersion> Versions { get; init; }
    }
}
