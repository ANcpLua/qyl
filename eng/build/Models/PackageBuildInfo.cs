// Extracted from open-telemetry/opentelemetry-dotnet-instrumentation (Apache-2.0).

using System;
using System.Collections.Generic;

namespace Qyl.Build.Models;

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
