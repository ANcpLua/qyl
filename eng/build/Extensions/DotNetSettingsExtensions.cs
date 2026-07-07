// Extracted from open-telemetry/opentelemetry-dotnet-instrumentation (Apache-2.0).
// qyl adaptation: target frameworks are plain strings (qyl is single-TFM today);
// the matrix helpers stay so multi-version library testing can be wired later.

using System.Collections.Generic;
using System.Linq;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Qyl.Build.Models;

namespace Qyl.Build;

internal static class DotNetSettingsExtensions
{
    public static DotNetPublishSettings SetTargetPlatformAnyCPU(this DotNetPublishSettings settings)
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static DotNetTestSettings SetTargetPlatformAnyCPU(this DotNetTestSettings settings)
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static DotNetMSBuildSettings SetTargetPlatformAnyCPU(this DotNetMSBuildSettings settings)
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static DotNetPublishSettings SetTargetPlatform(this DotNetPublishSettings settings, MSBuildTargetPlatform? platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }

    public static DotNetTestSettings SetTargetPlatform(this DotNetTestSettings settings, MSBuildTargetPlatform? platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }

    public static DotNetMSBuildSettings SetTargetPlatform(this DotNetMSBuildSettings settings, MSBuildTargetPlatform? platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }

    public static DotNetTestSettings EnableTrxLogOutput(this DotNetTestSettings settings, string resultsDirectory)
    {
        return settings
            .AddLoggers("trx")
            .SetResultsDirectory(resultsDirectory);
    }

    /// <summary>
    ///     Fans a build invocation out over a package version matrix: one settings instance per
    ///     <see cref="PackageBuildInfo"/> whose supported frameworks include <paramref name="targetFramework"/>
    ///     (a null target framework matches every entry).
    /// </summary>
    public static DotNetBuildSettings[] CombineWithBuildInfos(this DotNetBuildSettings settings, IReadOnlyCollection<PackageBuildInfo> buildInfos, string? targetFramework = null)
    {
        // NOTE: SetProperty creates internally a new instance!
        return settings.CombineWith(
            buildInfos.Where(buildInfo => targetFramework is null || buildInfo.SupportedFrameworks.Length == 0 || buildInfo.SupportedFrameworks.Contains(targetFramework)),
            (p, buildInfo) =>
            {
                p = p.SetProperty("LibraryVersion", buildInfo.LibraryVersion);

                foreach (var item in buildInfo.AdditionalMetaData)
                {
                    p = p.SetProperty(item.Key, item.Value);
                }

                if (buildInfo.SupportedFrameworks.Length > 0)
                {
                    p = p.SetProperty("TargetFrameworks", "\"\"\"" + string.Join(";", buildInfo.SupportedFrameworks) + "\"\"\"");
                }

                return p;
            });
    }

    public static DotNetRestoreSettings[] CombineWithBuildInfos(this DotNetRestoreSettings settings, IReadOnlyCollection<PackageBuildInfo> buildInfos)
    {
        // NOTE: SetProperty creates internally a new instance!
        return settings.CombineWith(buildInfos, (p, buildInfo) =>
        {
            p = p.SetProperty("LibraryVersion", buildInfo.LibraryVersion);

            foreach (var item in buildInfo.AdditionalMetaData)
            {
                p = p.SetProperty(item.Key, item.Value);
            }

            if (buildInfo.SupportedFrameworks.Length > 0)
            {
                p = p.SetProperty("TargetFrameworks", string.Join(";", buildInfo.SupportedFrameworks));
            }

            return p;
        });
    }

    private static string GetTargetPlatform(MSBuildTargetPlatform platform) =>
        platform == MSBuildTargetPlatform.MSIL ? "AnyCPU" : platform.ToString();
}
