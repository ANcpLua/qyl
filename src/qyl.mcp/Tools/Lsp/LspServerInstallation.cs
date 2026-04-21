// Copyright (c) 2025-2026 ancplua

using System.Runtime.InteropServices;

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     Resolves an LSP server binary location on disk using PATH and well-known
///     .NET global-tool install paths.
/// </summary>
internal sealed class LspServerInstallation
{
    /// <summary>
    ///     Locates the binary for a known server definition.
    /// </summary>
    /// <param name="definition">Server definition describing executable name and install command.</param>
    /// <returns>Absolute path to the binary.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown with the install command embedded when the binary cannot be located on PATH or in
    ///     the dotnet global-tool directory.
    /// </exception>
    public string Locate(LspServerDefinition definition)
    {
        var executableName = ResolveExecutableName(definition.Executable);

        foreach (var candidate in EnumerateCandidates(executableName))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            $"LSP server '{definition.Id}' not found on PATH or in dotnet tools directory. " +
            $"Install with: {definition.InstallCommand}");
    }

    private static string ResolveExecutableName(string baseName) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? baseName + ".exe" : baseName;

    private static IEnumerable<string> EnumerateCandidates(string executableName)
    {
        // 1. PATH (including the .dotnet/tools directory appended by the dotnet SDK)
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var directory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return Path.Combine(directory, executableName);
            }
        }

        // 2. Well-known dotnet global-tool paths (in case PATH isn't set for the host process)
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            yield return Path.Combine(userProfile, ".dotnet", "tools", executableName);
        }

        var dotnetCliHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME");
        if (!string.IsNullOrEmpty(dotnetCliHome))
        {
            yield return Path.Combine(dotnetCliHome, ".dotnet", "tools", executableName);
        }
    }
}
