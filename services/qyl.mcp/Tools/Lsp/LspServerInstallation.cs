
using System.Runtime.InteropServices;

namespace qyl.mcp.Tools.Lsp;

internal static class LspServerInstallation
{
    public static string Locate(LspServerDefinition definition)
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
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var directory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return Path.Combine(directory, executableName);
            }
        }

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
