
namespace qyl.mcp.Tools.Lsp;

internal sealed record LspServerResolutionResult(
    LspServerDefinition Definition,
    string BinaryPath,
    string WorkspaceRoot);

internal sealed class LspServerResolution(
    LspServerDefinitions definitions,
    LspLanguageMappings mappings)
{
    public LspServerResolutionResult Resolve(string filePath)
    {
        if (!Path.IsPathFullyQualified(filePath))
            throw new ArgumentException($"LSP tools require an absolute path. Got: {filePath}", nameof(filePath));

        if (!mappings.TryResolve(filePath, out var serverId))
        {
            var known = string.Join(", ", mappings.KnownExtensions);
            throw new NotSupportedException(
                $"No LSP server is registered for extension '{Path.GetExtension(filePath)}'. " +
                $"Known extensions: {known}");
        }

        if (!definitions.TryGet(serverId, out var definition))
        {
            var known = string.Join(", ", definitions.KnownIds);
            throw new InvalidOperationException(
                $"Language mapping yielded server id '{serverId}' but no catalog entry was found. " +
                $"Known ids: {known}");
        }

        var binaryPath = LspServerInstallation.Locate(definition);
        var workspaceRoot = WorkspaceRootWalker.FindRoot(filePath);
        return new LspServerResolutionResult(definition, binaryPath, workspaceRoot);
    }
}

internal static class WorkspaceRootWalker
{
    private static readonly string[] s_anchorFileExtensions = [".slnx", ".sln"];
    private static readonly string[] s_anchorProjectFileExtensions = [".csproj"];
    private static readonly string[] s_anchorFileNames = ["package.json"];

    public static string FindRoot(string filePath)
    {
        var file = new FileInfo(filePath);
        var directoryPath = file.Exists
            ? file.DirectoryName ?? throw new InvalidOperationException("Expected file path to have a directory.")
            : filePath;
        var directory = new DirectoryInfo(directoryPath);

        string? projectLevelRoot = null;

        while (directory is not null)
        {
            if (ContainsAny(directory, s_anchorFileExtensions, true))
                return directory.FullName;

            if (ContainsAny(directory, s_anchorFileNames, false))
                return directory.FullName;

            if (projectLevelRoot is null && ContainsAny(directory, s_anchorProjectFileExtensions, true))
                projectLevelRoot = directory.FullName;

            directory = directory.Parent;
        }

        return projectLevelRoot ?? Path.GetDirectoryName(filePath) ?? filePath;
    }

    private static bool ContainsAny(DirectoryInfo directory, string[] tokens, bool isExtension)
    {
        foreach (var token in tokens)
        {
            var pattern = isExtension ? "*" + token : token;
            if (directory.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly).Any())
                return true;
        }

        return false;
    }
}
