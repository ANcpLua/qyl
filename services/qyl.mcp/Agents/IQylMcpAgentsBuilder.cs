// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace qyl.mcp.Agents;

/// <summary>
///     One factory method per bounded qyl.mcp tool agent. Each call returns a fully
///     composed <see cref="AIAgent" /> wrapped with <c>UseQylAgentTelemetry()</c>, so
///     the <c>QYL0135</c> analyzer is satisfied at the construction site.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="IsConfigured" /> reflects whether the upstream
///         <see cref="qyl.mcp.Clients.IQylMcpChatClientBuilder" /> (qyl.mcp's
///         equivalent of qyl.loom's <c>IQylLoomChatClientBuilder</c> — qyl.mcp
///         doesn't reference qyl.loom) resolved a non-null <see cref="IChatClient" />.
///         Each <c>Build*Agent</c> method throws <see cref="InvalidOperationException" />
///         when <see cref="IsConfigured" /> is <see langword="false" />.
///     </para>
///     <para>
///         <see cref="BuildRcaAgent" /> and <see cref="BuildUseQylAgent" /> accept a
///         pre-curated <see cref="AITool" /> set because the meta-tool's allowed
///         tool surface is computed per-call (filtered by
///         <c>QylToolManifest.CreateTools</c> + <c>InvestigationGuard.Wrap</c>).
///     </para>
/// </remarks>
internal interface IQylMcpAgentsBuilder
{
    /// <summary>
    ///     <see langword="true" /> when an LLM provider is configured and the
    ///     <c>Build*Agent</c> methods can return non-null instances.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Summary tool — produces a structured AI summary of a qyl error issue.</summary>
    AIAgent BuildSummarizeErrorAgent();

    /// <summary>Summary tool — produces a structured AI summary of a distributed trace.</summary>
    AIAgent BuildSummarizeTraceAgent();

    /// <summary>Summary tool — produces a structured AI summary of a session's spans and lifecycle.</summary>
    AIAgent BuildSummarizeSessionAgent();

    /// <summary>Test-generation tool — emits a regression test that would catch a qyl error if it reoccurs.</summary>
    AIAgent BuildTestGenerationAgent();

    /// <summary>Assisted-query tool — translates natural-language observability questions into DuckDB SELECT queries.</summary>
    /// <param name="rowLimit">Maximum rows the generated query is allowed to return — encoded into the system prompt.</param>
    AIAgent BuildAssistedQueryAgent(int rowLimit);

    /// <summary>RCA meta-tool — multi-phase root-cause investigator with curated tools under <c>InvestigationGuard</c>.</summary>
    /// <param name="tools">Pre-curated, guard-wrapped tools the agent is allowed to call.</param>
    AIAgent BuildRcaAgent(IReadOnlyList<AITool> tools);

    /// <summary>Use-qyl meta-tool — orchestrates curated qyl MCP tools to answer observability questions.</summary>
    /// <param name="tools">Pre-curated, guard-wrapped tools the agent is allowed to call.</param>
    AIAgent BuildUseQylAgent(IReadOnlyList<AITool> tools);
}
