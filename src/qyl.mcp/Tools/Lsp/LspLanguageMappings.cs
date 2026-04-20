// Copyright (c) 2025-2026 ancplua

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     Maps file extensions to LSP server ids. Matching is case-insensitive on the extension.
///     Phase 1 covers C# only — TypeScript/JavaScript is Phase 2.
/// </summary>
internal sealed class LspLanguageMappings
{
    private readonly Dictionary<string, string> _extensionToServerId = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp-ls", [".csproj"] = "csharp-ls", [".slnx"] = "csharp-ls", [".sln"] = "csharp-ls"
    };

    /// <summary>
    ///     Enumerate known file extensions for diagnostic messages.
    /// </summary>
    public IReadOnlyCollection<string> KnownExtensions => _extensionToServerId.Keys;

    /// <summary>
    ///     Resolves a server id from a file path extension.
    /// </summary>
    /// <param name="filePath">Absolute file path.</param>
    /// <param name="serverId">The matching server id, when found.</param>
    /// <returns><c>true</c> when the file extension is recognized.</returns>
    public bool TryResolve(string filePath, out string serverId)
    {
        var extension = Path.GetExtension(filePath);
        if (_extensionToServerId.TryGetValue(extension, out var value))
        {
            serverId = value;
            return true;
        }

        serverId = string.Empty;
        return false;
    }
}
