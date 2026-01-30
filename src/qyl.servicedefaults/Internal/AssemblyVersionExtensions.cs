// =============================================================================
// Assembly Version Extensions - OTel Enterprise Pattern
// Based on: opentelemetry-dotnet-contrib/src/Shared/AssemblyVersionExtensions.cs
// =============================================================================

using System.Reflection;
using ANcpLua.Roslyn.Utilities;

namespace Qyl.ServiceDefaults.Internal;

/// <summary>
/// Extension methods for extracting version information from assemblies.
/// </summary>
internal static class AssemblyVersionExtensions
{
    /// <summary>
    /// Gets the package version from the assembly's informational version attribute.
    /// </summary>
    /// <remarks>
    /// Strips the git SHA after the '+' sign for cleaner version strings in telemetry.
    /// Ex: 1.5.0-alpha.1.40+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4 -> 1.5.0-alpha.1.40
    /// </remarks>
    public static string GetPackageVersion(this Assembly assembly)
    {
        Debug.Assert(assembly is not null, "assembly was null");
        var informationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Debug.Assert(!string.IsNullOrEmpty(informationalVersion),
            "AssemblyInformationalVersionAttribute was not found");
        return ParsePackageVersion(informationalVersion);
    }

    /// <summary>
    /// Tries to get the package version from the assembly's informational version attribute.
    /// </summary>
    public static bool TryGetPackageVersion(this Assembly assembly, [NotNullWhen(true)] out string? packageVersion)
    {
        var informationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informationalVersion))
        {
            packageVersion = null;
            return false;
        }

        packageVersion = ParsePackageVersion(informationalVersion);
        return true;
    }

    private static string ParsePackageVersion(string informationalVersion)
    {
        // Strip git SHA after '+' sign
        var indexOfPlusSign = informationalVersion.IndexOfOrdinal("+");
        return indexOfPlusSign > 0
            ? informationalVersion[..indexOfPlusSign]
            : informationalVersion;
    }
}
