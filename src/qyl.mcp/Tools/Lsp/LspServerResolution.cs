// Copyright (c) 2025-2026 ancplua

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     Resolved launch parameters for a single LSP server + workspace pairing.
/// </summary>
/// <param name="Definition">The server definition (id, executable, args).</param>
/// <param name="BinaryPath">Absolute path to the resolved binary.</param>
/// <param name="WorkspaceRoot">Absolute path to the workspace root (solution / project / repo root).</param>
internal sealed record LspServerResolutionResult(
    LspServerDefinition Definition,
    string BinaryPath,
    string WorkspaceRoot);

/// <summary>
///     Joins the language-to-server map, the server catalog, and binary discovery into a
///     single lookup that answers: "given this file, which server, which binary, which
///     workspace root?". Also enforces the absolute-path and workspace-root hard rules.
/// </summary>
internal sealed class LspServerResolution(
    LspServerDefinitions definitions,
    LspLanguageMappings mappings,
    LspServerInstallation installation)
{
    /// <summary>
    ///     Resolve launch parameters for an absolute file path.
    /// </summary>
    /// <param name="filePath">Absolute path to a file inside the workspace.</param>
    /// <returns>Resolved launch parameters (definition, binary path, workspace root).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath" /> is not absolute.</exception>
    /// <exception cref="NotSupportedException">
    ///     Thrown when the file extension does not map to any known LSP server.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the resolved server has no catalog entry or the binary cannot be located.
    /// </exception>
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

        var binaryPath = installation.Locate(definition);
        var workspaceRoot = WorkspaceRootWalker.FindRoot(filePath);
        return new LspServerResolutionResult(definition, binaryPath, workspaceRoot);
    }
}

/// <summary>
///     Walks up from a file path looking for an anchor (<c>.slnx</c>, <c>.sln</c>, <c>.csproj</c>,
///     <c>package.json</c>) that marks a workspace root. Falls back to the file's directory when
///     no anchor is found.
/// </summary>
internal static class WorkspaceRootWalker
{
    private static readonly string[] AnchorFileExtensions = [".slnx", ".sln"];
    private static readonly string[] AnchorProjectFileExtensions = [".csproj"];
    private static readonly string[] AnchorFileNames = ["package.json"];

    /// <summary>
    ///     Walks parents of <paramref name="filePath" /> to find the nearest anchor that marks a
    ///     workspace root. Prefers solution-level anchors over project-level anchors.
    /// </summary>
    /// <param name="filePath">Absolute file path.</param>
    /// <returns>Absolute path to the workspace root directory.</returns>
    public static string FindRoot(string filePath)
    {
        var directory = new DirectoryInfo(
            File.Exists(filePath) ? Path.GetDirectoryName(filePath)! : filePath);

        string? projectLevelRoot = null;

        while (directory is not null)
        {
            // Prefer solution-level anchors.
            if (ContainsAny(directory, AnchorFileExtensions, isExtension: true))
                return directory.FullName;

            if (ContainsAny(directory, AnchorFileNames, isExtension: false))
                return directory.FullName;

            // Remember the deepest project-level anchor as a fallback.
            if (projectLevelRoot is null && ContainsAny(directory, AnchorProjectFileExtensions, isExtension: true))
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
