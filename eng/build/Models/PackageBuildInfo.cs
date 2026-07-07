// Extracted from open-telemetry/opentelemetry-dotnet-instrumentation (Apache-2.0).

using System;
using System.Collections.Generic;

namespace Qyl.Build.Models;

/// <summary>
///     One row of a package build/test matrix: a library version plus the target
///     frameworks, platforms, and extra MSBuild properties it applies to. Consumed by
///     <c>DotNetSettingsExtensions.CombineWithBuildInfos</c> to fan a single
///     build/restore invocation out over every matrix entry.
/// </summary>
public sealed class PackageBuildInfo
{
    public PackageBuildInfo(string libraryVersion, string[]? supportedFrameworks = null, string[]? supportedPlatforms = null, Dictionary<string, string>? additionalMetaData = null)
    {
        LibraryVersion = libraryVersion;
        SupportedFrameworks = supportedFrameworks ?? [];
        SupportedPlatforms = supportedPlatforms ?? [];
        AdditionalMetaData = additionalMetaData ?? [];
    }

    public string LibraryVersion { get; }

    public string[] SupportedFrameworks { get; }

    public string[] SupportedPlatforms { get; }

    public Dictionary<string, string> AdditionalMetaData { get; }
}
