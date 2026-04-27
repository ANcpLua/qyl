// Copyright (c) 2025-2026 ancplua

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using qyl.mcp.Agents;

namespace qyl.mcp.Clients;

/// <summary>
///     Default <see cref="IQylMcpChatClientBuilder" />. Resolves the agent
///     <see cref="IChatClient" /> from <see cref="AgentLlmFactory" /> (which already
///     applies <c>WithQylTelemetry</c> at the chat-client layer) and exposes a
///     pipeline-form variant for meta-tools that need
///     <c>UseFunctionInvocation</c> with qyl-specific tuning.
/// </summary>
internal sealed class QylMcpChatClientBuilder(IConfiguration config) : IQylMcpChatClientBuilder
{
    private readonly IChatClient? _llm = AgentLlmFactory.TryCreate(config);

    /// <inheritdoc />
    public bool IsConfigured => _llm is not null;

    /// <inheritdoc />
    public IChatClient? BuildChatClient() => _llm;

    /// <inheritdoc />
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
