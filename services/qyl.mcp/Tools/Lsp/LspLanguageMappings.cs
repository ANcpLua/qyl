
namespace qyl.mcp.Tools.Lsp;

internal sealed class LspLanguageMappings
{
    private readonly Dictionary<string, string> _extensionToServerId = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp-ls", [".csproj"] = "csharp-ls", [".slnx"] = "csharp-ls", [".sln"] = "csharp-ls"
    };

    public IReadOnlyCollection<string> KnownExtensions => _extensionToServerId.Keys;

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
