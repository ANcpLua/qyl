using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using qyl.mcp.Agents;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tool that performs AI-powered multi-step root cause analysis.
///     Creates an embedded agent with access to error, anomaly, storage, and log tools
///     to autonomously investigate error issues.
/// </summary>
[McpServerToolType]
internal sealed class RcaTools(IServiceProvider services, IConfiguration config)
{
    private readonly IChatClient? _llm = AgentLlmFactory.TryCreate(config);

    [McpServerTool(Name = "qyl.root_cause_analysis", Title = "Root Cause Analysis",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("""
                 Perform AI-powered root cause analysis on an error issue.

                 Uses a multi-phase investigation approach:
                 1. Error Characterization -- understand the error
                 2. Correlation -- cross-reference with traces, logs, metrics
                 3. Root Cause Identification -- synthesize findings

                 The agent has access to error, anomaly, storage, and log tools
                 to autonomously investigate the issue.

                 Requires QYL_AGENT_API_KEY to be configured.

                 Returns: Structured RCA report with root cause, evidence,
                          timeline, recommendations, and confidence level
                 """)]
    public async Task<string> RootCauseAnalysisAsync(
        [Description("The error issue ID to investigate")] string issueId,
        [Description("Additional context: time range, suspected cause, recent deploys")] string? context = null,
        CancellationToken ct = default)
    {
        if (_llm is null)
            return "Root cause analysis requires an LLM provider. " +
                   "Set QYL_AGENT_API_KEY and QYL_AGENT_MODEL environment variables. " +
                   "Use the individual error and anomaly tools instead.";

        // Build curated tool set -- only data-retrieval tools, not LLM tools
        List<AIFunction> tools = DiscoverToolsFrom(
            typeof(ErrorTools),
            typeof(AnomalyTools),
            typeof(SpanQueryTools),
            typeof(StructuredLogTools));

        IChatClient agent = new ChatClientBuilder(_llm)
            .UseFunctionInvocation(configure: static invoker =>
            {
                invoker.MaximumIterationsPerRequest = 10;
                invoker.AllowConcurrentInvocation = false;
            })
            .Build();

        string userMessage = $"Investigate error issue ID: {issueId}";
        if (context is not null)
            userMessage += $"\n\nAdditional context: {context}";

        List<ChatMessage> messages =
        [
            new(ChatRole.System, RcaPrompt.Prompt),
            new(ChatRole.User, userMessage)
        ];

        ChatOptions options = new() { Tools = [.. tools] };

        try
        {
            ChatResponse response = await agent.GetResponseAsync(messages, options, ct).ConfigureAwait(false);
            return response.Text ?? "RCA completed with no output.";
        }
        catch (HttpRequestException ex)
        {
            return $"Error during root cause analysis: {ex.Message}";
        }
    }

    private List<AIFunction> DiscoverToolsFrom(params Type[] toolTypes)
    {
        List<AIFunction> tools = [];
        foreach (Type type in toolTypes)
        {
            object instance = services.GetRequiredService(type);
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
