using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using qyl.mcp.Tools;
using Qyl.Generated;

namespace qyl.mcp.Agents;

/// <summary>
///     Discovers all [McpServerTool] methods on registered tool classes and wraps
///     them as <see cref="AIFunction" /> for use by the embedded meta-agent (use_qyl).
///     Excludes tools that would cause recursion or LLM-in-LLM loops.
/// </summary>
/// <remarks>
///     Tool types are discovered at compile time by the ToolManifestEmitter
///     (source generator) via <see cref="QylToolManifest.ToolTypes" />.
/// </remarks>
internal sealed class McpToolRegistry(IServiceProvider services)
{
    private IReadOnlyList<AIFunction>? _cachedTools;

    /// <summary>
    ///     Returns all discovered MCP tool methods as AIFunction instances.
    ///     Results are cached after first call (tool set is static at runtime).
    /// </summary>
    public IReadOnlyList<AIFunction> GetTools() => _cachedTools ??= DiscoverTools();

    private List<AIFunction> DiscoverTools()
    {
        var tools = new List<AIFunction>();

        foreach (var type in QylToolManifest.ToolTypes)
        {
            // Skip UseQylTools — it's the meta-agent itself; including it would cause recursion.
            if (type == typeof(UseQylTools))
                continue;

            // Skill gating may not register all manifest types — degrade to enabled subset.
            var instance = services.GetService(type);
            if (instance is null)
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<McpServerToolAttribute>() is not { } attr)
                    continue;

                var name = attr.Name ?? method.Name;
                tools.Add(AIFunctionFactory.Create(method, instance,
                    new AIFunctionFactoryOptions { Name = name }));
            }
        }

        return tools;
    }
}
