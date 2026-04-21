using System.ComponentModel;
using qyl.mcp.Agents;
using qyl.mcp.Formatting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;
using Qyl.Generated;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP meta-tool that gives an embedded LLM agent access to ALL qyl tools.
///     Follows the Sentry "use_sentry" pattern: the calling agent (Claude/Cursor)
///     sends a natural language query, the embedded agent autonomously calls
///     the right tools, and returns the results.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Agent)]
internal sealed class UseQylTools(IServiceProvider services, IConfiguration config)
{
    private readonly IChatClient? _agentClient = AgentLlmFactory.TryCreate(config);

    [QylCapability("agentic_investigation")]
    [McpServerTool(Name = "qyl.use_qyl", Title = "Use qyl",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("""
                 Ask qyl's AI agent to answer complex observability questions.
                 The agent has access to ALL qyl tools and will autonomously
                 query spans, traces, logs, builds, analytics, GenAI usage,
                 sessions, services, and more to answer your question.

                 Use this for questions that span multiple data domains or
                 require multi-step investigation.

                 Examples:
                 - "What caused the spike in errors at 3pm?"
                 - "Compare GenAI costs across services this week"
                 - "Show me the slowest traces and their related build failures"
                 - "Summarize system health across all services"

                 Returns: Investigation results from the meta-agent
                 """)]
    public async Task<string> UseQylAsync(
        [Description("Your question about observability data")]
        string question,
        [Description("Additional context: service name, time range hint, session ID, etc.")]
        string? context = null,
        CancellationToken ct = default)
    {
        if (_agentClient is null)
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
            .ToList();

        var client = new ChatClientBuilder(_agentClient)
            .UseFunctionInvocation(configure: invoker =>
            {
                invoker.MaximumIterationsPerRequest = 10;
                invoker.AllowConcurrentInvocation = false;
            })
            .Build();

        var userMessage = context is not null
            ? $"{question}\n\nContext: {context}"
            : question;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, UseQylSystemPrompt.Prompt), new(ChatRole.User, userMessage)
        };

        var options = new ChatOptions { Tools = [.. guardedTools] };

        try
        {
            var response = await client.GetResponseAsync(messages, options, ct).ConfigureAwait(false);
            var output = response.Text ?? "Agent completed with no output. Try rephrasing your question.";

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
