// Copyright (c) 2025-2026 ancplua

using Microsoft.Extensions.AI;

namespace Qyl.Loom.Clients;

/// <summary>
///     Provider-agnostic <see cref="IChatClient" /> factory for qyl.loom production
///     services. The composition root calls through this seam so any concrete provider
///     can be swapped without touching downstream agent code.
/// </summary>
/// <remarks>
///     <para>
///         The qyl.loom.patterns sibling exposes the same shape with stage-keyed
///         <see cref="ANcpLua.Agents.Testing.ChatClients.FakeChatClient" /> instances;
///         this production variant resolves the DI-injected provider client.
///     </para>
/// </remarks>
internal interface IQylLoomChatClientBuilder
{
    /// <summary>
    ///     Returns the configured <see cref="IChatClient" /> for production agent
    ///     construction. <see langword="null" /> when no LLM provider is configured
    ///     (callers must fall back to heuristics or short-circuit the agent path).
    /// </summary>
    IChatClient? BuildChatClient();
}
