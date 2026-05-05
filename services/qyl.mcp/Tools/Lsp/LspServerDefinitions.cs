
using System.Diagnostics.CodeAnalysis;

namespace qyl.mcp.Tools.Lsp;

internal sealed record LspServerDefinition(
    string Id,
    string Executable,
    IReadOnlyList<string> Arguments,
    string InstallCommand,
    int FirstInitTimeoutSeconds);

internal sealed class LspServerDefinitions
{
    private static readonly LspServerDefinition s_cSharpLs = new(
        "csharp-ls",
        "csharp-ls",
        [],
        "dotnet tool install -g csharp-ls",
        90);

    private readonly Dictionary<string, LspServerDefinition> _byId = new(StringComparer.Ordinal)
    {
        [s_cSharpLs.Id] = s_cSharpLs
    };

    public IReadOnlyCollection<string> KnownIds => _byId.Keys;

    public bool TryGet(string id, [NotNullWhen(true)] out LspServerDefinition? definition)
    {
        if (_byId.TryGetValue(id, out var value))
        {
            definition = value;
            return true;
        }

        definition = null;
        return false;
    }
}
