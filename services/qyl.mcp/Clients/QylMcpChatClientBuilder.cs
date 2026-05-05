
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using qyl.mcp.Agents;

namespace qyl.mcp.Clients;

internal sealed class QylMcpChatClientBuilder(IConfiguration config) : IQylMcpChatClientBuilder
{
    private readonly IChatClient? _llm = AgentLlmFactory.TryCreate(config);

    public bool IsConfigured => _llm is not null;

    public IChatClient? BuildChatClient() => _llm;

    public IChatClient? BuildAgentChatClient(int maximumIterationsPerRequest = 10)
    {
        if (_llm is null) return null;

        return new ChatClientBuilder(_llm)
            .UseFunctionInvocation(configure: invoker =>
            {
                invoker.MaximumIterationsPerRequest = maximumIterationsPerRequest;
                invoker.AllowConcurrentInvocation = false;
            })
            .Build();
    }
}
