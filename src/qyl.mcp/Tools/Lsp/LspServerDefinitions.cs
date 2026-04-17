// Copyright (c) 2025-2026 ancplua

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     Immutable descriptor for a supported LSP server. Phase 1 ships <c>csharp-ls</c> only.
///     Phase 2 will add <c>typescript-language-server</c>.
/// </summary>
/// <param name="Id">Stable identifier (e.g. <c>csharp-ls</c>).</param>
/// <param name="Executable">Executable name on PATH. Resolved at runtime by <see cref="LspServerInstallation" />.</param>
/// <param name="Arguments">Command-line arguments passed to the executable (for example <c>--stdio</c>).</param>
/// <param name="InstallCommand">Human-readable install hint embedded in the error when the binary is missing.</param>
/// <param name="FirstInitTimeoutSeconds">
///     First-call budget — <c>csharp-ls</c> can take 10-30s on large repos. Subsequent
///     requests should complete in seconds.
/// </param>
internal sealed record LspServerDefinition(
    string Id,
    string Executable,
    IReadOnlyList<string> Arguments,
    string InstallCommand,
    int FirstInitTimeoutSeconds);

/// <summary>
///     Static catalog of known LSP server definitions. Phase 1 hosts C# only; TypeScript is
///     Phase 2. DI exposes <see cref="LspServerDefinitions" /> as a singleton; callers use
///     <see cref="TryGet" /> indirectly via <see cref="LspServerResolution" />.
/// </summary>
internal sealed class LspServerDefinitions
{
    private static readonly LspServerDefinition CSharpLs = new(
        Id: "csharp-ls",
        Executable: "csharp-ls",
        Arguments: [],
        InstallCommand: "dotnet tool install -g csharp-ls",
        FirstInitTimeoutSeconds: 90);

    private readonly Dictionary<string, LspServerDefinition> _byId = new(StringComparer.Ordinal)
    {
        [CSharpLs.Id] = CSharpLs,
    };

    /// <summary>
    ///     Look up a server definition by its id.
    /// </summary>
    /// <param name="id">Server id (case-sensitive).</param>
    /// <param name="definition">The matching definition, when found.</param>
    /// <returns><c>true</c> when a definition exists.</returns>
    public bool TryGet(string id, out LspServerDefinition definition)
    {
        if (_byId.TryGetValue(id, out var value))
        {
            definition = value;
            return true;
        }

        definition = null!;
        return false;
    }

    /// <summary>
    ///     Enumerate known server ids. Used in diagnostic error messages.
    /// </summary>
    public IReadOnlyCollection<string> KnownIds => _byId.Keys;
}
