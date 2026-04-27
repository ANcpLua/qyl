using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Generated;
using qyl.mcp.Agents;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools;

/// <summary>
///     Meta-tool: embedded <see cref="ChatClientAgent" /> answers natural-language observability questions by
///     autonomously calling every other qyl MCP tool under an <see cref="InvestigationLineage" /> guard.
///     <c>UseFunctionInvocation</c> is wired at the chat-client layer (not the agent layer) by the
///     <see cref="IQylMcpAgentsBuilder" /> so qyl's non-default <c>MaximumIterationsPerRequest</c> +
///     <c>AllowConcurrentInvocation=false</c> take effect — <see cref="ChatClientAgent" /> only inserts a default
///     invoker when none is present.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Agent)]
internal sealed partial class UseQylTools(IServiceProvider services, IQylMcpAgentsBuilder agents)
{

    [QylCapability("agentic_investigation")]
    [McpServerTool(Name = "qyl.use_qyl", Title = "Use qyl",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> UseQylAsync(
        string question,
        string? context = null,
        CancellationToken ct = default)
    {
        if (!agents.IsConfigured)
        {
            return "use_qyl requires an LLM provider. " +
                   "Set QYL_AGENT_API_KEY and QYL_AGENT_MODEL environment variables. " +
                   "Use the individual query tools (search_spans, list_errors, get_genai_stats) instead.";
        }

        var lineageResult = InvestigationLineage.TryEnter();
        if (!lineageResult.IsAllowed)
            return lineageResult.RefusalReason!;

        var lineage = lineageResult.Lineage!;
        var investigation = InvestigationGuard.FromEnvironment();

        var guardedTools = QylToolManifest
            .CreateTools(services, static type => type != typeof(UseQylTools))
            .Select(tool => (AITool)investigation.Wrap(tool))
            .ToArray();

        var agent = agents.BuildUseQylAgent(guardedTools);

        var userMessage = context is not null
            ? $"{question}\n\nContext: {context}"
            : question;

        try
        {
            var response = await agent.RunAsync(userMessage, cancellationToken: ct).ConfigureAwait(false);
            var output = response.Text;

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
            return $"Error connecting to LLM provider: {ex.Message}";
        }
        finally
        {
            lineage.Complete();
        }
    }
}
