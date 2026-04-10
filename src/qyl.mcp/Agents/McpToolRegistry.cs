using Microsoft.Extensions.AI;
using qyl.mcp.Tools;
using Qyl.Generated;

namespace qyl.mcp.Agents;

/// <summary>
///     Provides all discovered MCP tool methods as AIFunction instances
///     for use by the embedded meta-agent (use_qyl).
///     Excludes tools that would cause recursion or LLM-in-LLM loops.
/// </summary>
/// <remarks>
///     Tool discovery is performed at compile time by the ToolManifestGenerator.
///     No runtime reflection is used.
/// </remarks>
internal sealed class McpToolRegistry(IServiceProvider services)
{
    private IReadOnlyList<AIFunction>? _cachedTools;

    /// <summary>
    ///     Returns all discovered MCP tool methods as AIFunction instances.
    ///     Results are cached after first call (tool set is static at runtime).
    /// </summary>
    public IReadOnlyList<AIFunction> GetTools() =>
        _cachedTools ??= QylToolManifest.CreateTools(services,
            static type => type != typeof(UseQylTools));
}
