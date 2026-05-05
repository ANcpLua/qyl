using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Generated;
using qyl.mcp.Agents;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Agent)]
internal sealed partial class RcaTools(IServiceProvider services, IQylMcpAgentsBuilder agents)
{

    [QylCapability("trace_investigation", QylCapabilityRole.FollowUp)]
    [QylCapability("agentic_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.root_cause_analysis", Title = "Root Cause Analysis",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> RootCauseAnalysisAsync(
        string issueId,
        string? context = null,
        CancellationToken ct = default)
    {
        if (!agents.IsConfigured)
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

        var guardedTools = DiscoverToolsFrom(
                typeof(ErrorTools),
                typeof(AnomalyTools),
                typeof(SpanQueryTools),
                typeof(StructuredLogTools))
            .Select(tool => (AITool)investigation.Wrap(tool))
            .ToArray();

        var agent = agents.BuildRcaAgent(guardedTools);

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
