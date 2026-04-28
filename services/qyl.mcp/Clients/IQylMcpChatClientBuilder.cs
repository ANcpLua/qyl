// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace qyl.mcp.Clients;

/// <summary>
///     Provider-agnostic <see cref="IChatClient" /> factory for qyl.mcp tool agents.
///     Tools resolve their chat client through this builder so the underlying provider
///     (qyl agent LLM factory + <c>WithQylTelemetry</c>) can be swapped or stubbed
///     without touching individual tool code.
/// </summary>
internal interface IQylMcpChatClientBuilder
{
    /// <summary>
    ///     <see langword="true" /> when an LLM provider is configured and the
    ///     <c>Build*</c> methods can return non-null instances.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    ///     Returns the configured agent <see cref="IChatClient" /> (already wrapped
    ///     with <c>WithQylTelemetry</c> at the chat-client layer). <see langword="null" />
    ///     when no provider is configured.
    /// </summary>
    IChatClient? BuildChatClient();

    /// <summary>
    ///     Returns the agent chat client wrapped with a configured
    ///     <c>UseFunctionInvocation</c> pipeline — qyl's non-default
    ///     <c>MaximumIterationsPerRequest</c> and
    ///     <c>AllowConcurrentInvocation = false</c> take effect at the chat-client
    ///     layer so <see cref="ChatClientAgent" /> doesn't insert its default invoker.
    ///     Used by meta-tools (<c>qyl.use_qyl</c>, <c>qyl.root_cause_analysis</c>)
    ///     that loop through curated tool sets.
    /// </summary>
    /// <param name="maximumIterationsPerRequest">Upper bound on tool-invocation iterations.</param>
    IChatClient? BuildAgentChatClient(int maximumIterationsPerRequest = 10);
}
