using System.ComponentModel;
using qyl.mcp.Agents;
using qyl.mcp.Formatting;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Generated;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tool that performs AI-powered multi-step root cause analysis.
///     Creates an embedded agent with access to error, anomaly, storage, and log tools
///     to autonomously investigate error issues.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Agent)]
internal sealed class RcaTools(IServiceProvider services, IConfiguration config)
{
    private readonly IChatClient? _llm = AgentLlmFactory.TryCreate(config);

    [QylCapability("trace_investigation", QylCapabilityRole.FollowUp)]
    [QylCapability("agentic_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.root_cause_analysis", Title = "Root Cause Analysis",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
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
        [Description("The error issue ID to investigate")]
        string issueId,
        [Description("Additional context: time range, suspected cause, recent deploys")]
        string? context = null,
        CancellationToken ct = default)
    {
        if (_llm is null)
        {
            return "Root cause analysis requires an LLM provider. " +
                   "Set QYL_AGENT_API_KEY and QYL_AGENT_MODEL environment variables. " +
                   "Use the individual error and anomaly tools instead.";
        }

        var lineageResult = InvestigationLineage.TryEnter();
        if (!lineageResult.IsAllowed)
            return lineageResult.RefusalReason!;

        var lineage = lineageResult.Lineage!;
        var investigation = InvestigationGuard.FromEnvironment(50);

        // Build curated tool set -- only data-retrieval tools, not LLM tools
        var guardedTools = DiscoverToolsFrom(
                typeof(ErrorTools),
                typeof(AnomalyTools),
                typeof(SpanQueryTools),
                typeof(StructuredLogTools))
            .Select(tool => (AITool)investigation.Wrap(tool));

        var agent = new ChatClientBuilder(_llm)
            .UseFunctionInvocation(configure: static invoker =>
            {
                invoker.MaximumIterationsPerRequest = 10;
                invoker.AllowConcurrentInvocation = false;
            })
            .Build()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "RcaAgent",
                Description = "Multi-phase root-cause investigator with access to error, anomaly, span, and structured-log tools.",
                ChatOptions = new ChatOptions
                {
                    Instructions = RcaPrompt.Prompt,
                    Tools = [.. guardedTools],
                    ToolMode = ChatToolMode.Auto,
                },
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

        var userMessage = $"Investigate error issue ID: {issueId}";
        if (context is not null)
            userMessage += $"\n\nAdditional context: {context}";

        try
        {
            var response = await agent.RunAsync(userMessage, cancellationToken: ct).ConfigureAwait(false);
            var output = response.Text is { Length: > 0 } text ? text : "RCA completed with no output.";

            output += ResponseFormatter.FormatToolCallTrace(
                investigation.ToolCallCounts, investigation.TotalCalls, investigation.MaxToolCalls);
            output += $"\n[Lineage: {lineage.FormatLineageSummary()}]";

            return output;
        }
        catch (OperationCanceledException) when (investigation.TotalCalls >= investigation.MaxToolCalls)
        {
            return investigation.BuildDiagnosticSummary();
        }
        catch (HttpRequestException ex)
        {
            return $"Error during root cause analysis: {ex.Message}";
        }
        finally
        {
            lineage.Complete();
        }
    }

    private List<AIFunction> DiscoverToolsFrom(params Type[] toolTypes)
    {
        var allowed = new HashSet<Type>(toolTypes);
        return QylToolManifest.CreateTools(services, allowed.Contains);
    }
}
