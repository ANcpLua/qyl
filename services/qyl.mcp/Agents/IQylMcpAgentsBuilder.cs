
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace qyl.mcp.Agents;

internal interface IQylMcpAgentsBuilder
{
    bool IsConfigured { get; }

    AIAgent BuildSummarizeErrorAgent();

    AIAgent BuildSummarizeTraceAgent();

    AIAgent BuildSummarizeSessionAgent();

    AIAgent BuildTestGenerationAgent();

    AIAgent BuildAssistedQueryAgent(int rowLimit);

    AIAgent BuildRcaAgent(IReadOnlyList<AITool> tools);

    AIAgent BuildUseQylAgent(IReadOnlyList<AITool> tools);
}
