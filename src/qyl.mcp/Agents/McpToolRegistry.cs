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
        typeof(StorageHealthTools),
        typeof(SpanQueryTools),
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
    public IReadOnlyList<AIFunction> GetTools() => _cachedTools ??= ToolDiscovery.Discover(services, ToolTypes);
}

/// <summary>
///     Discovers [McpServerTool] methods on DI-registered types and wraps them as <see cref="AIFunction"/>.
///     Skips types not registered in the container (skills may be disabled).
/// </summary>
internal static class ToolDiscovery
{
    public static List<AIFunction> Discover(IServiceProvider services, params Type[] toolTypes)
    {
        List<AIFunction> tools = [];
        foreach (Type type in toolTypes)
        {
            object? instance = services.GetService(type);
            if (instance is null) continue;

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<McpServerToolAttribute>() is not { } attr)
                    continue;

                string name = attr.Name ?? method.Name;
                tools.Add(AIFunctionFactory.Create(method, instance,
                    new AIFunctionFactoryOptions { Name = name }));
            }
        }

        return tools;
    }
}
