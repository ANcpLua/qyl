using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using qyl.mcp.Tools;

namespace qyl.mcp.Agents;

/// <summary>
///     Discovers all [McpServerTool] methods on registered tool classes and wraps
///     them as <see cref="AIFunction"/> for use by the embedded meta-agent (use_qyl).
///     Excludes tools that would cause recursion or LLM-in-LLM loops.
/// </summary>
internal sealed class McpToolRegistry(IServiceProvider services)
{
    /// <summary>
    ///     Tool class types whose [McpServerTool] methods are exposed to the meta-agent.
    ///     Excluded: UseQylTools (self), InvestigateTools (LLM-in-LLM), CopilotTools (LLM chat/workflow).
    /// </summary>
    private static readonly Type[] ToolTypes =
    [
        typeof(TelemetryTools),
        typeof(ReplayTools),
        typeof(ConsoleTools),
        typeof(StructuredLogTools),
        typeof(BuildTools),
        typeof(GenAiTools),
        typeof(StorageTools),
        typeof(AnalyticsTools),
        typeof(ClaudeCodeTools),
        typeof(ServiceTools),
        typeof(ErrorTools),
        typeof(AnomalyTools)
    ];

    private IReadOnlyList<AIFunction>? _cachedTools;

    /// <summary>
    ///     Returns all discovered MCP tool methods as AIFunction instances.
    ///     Results are cached after first call (tool set is static at runtime).
    /// </summary>
    public IReadOnlyList<AIFunction> GetTools() => _cachedTools ??= DiscoverTools();

    private List<AIFunction> DiscoverTools()
    {
        var tools = new List<AIFunction>();

        foreach (var type in ToolTypes)
        {
            var instance = services.GetRequiredService(type);

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
