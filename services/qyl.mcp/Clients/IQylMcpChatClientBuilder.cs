
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace qyl.mcp.Clients;

internal interface IQylMcpChatClientBuilder
{
    bool IsConfigured { get; }

    IChatClient? BuildChatClient();

    IChatClient? BuildAgentChatClient(int maximumIterationsPerRequest = 10);
}
