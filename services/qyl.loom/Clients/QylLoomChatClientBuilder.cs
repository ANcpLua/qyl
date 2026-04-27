// Copyright (c) 2025-2026 ancplua

using Microsoft.Extensions.AI;

namespace Qyl.Loom.Clients;

/// <summary>
///     Default <see cref="IQylLoomChatClientBuilder" />. Resolves the
///     <see cref="IChatClient" /> from DI; the host (composition root) is responsible
///     for the chat-client telemetry wrap (<c>WithQylTelemetry</c> /
///     <c>UseQylTelemetry</c>) before registration. Agent-layer wrapping is applied
///     by <see cref="Qyl.Loom.Agents.QylLoomAgentsBuilder" /> at construction.
/// </summary>
internal sealed class QylLoomChatClientBuilder(IChatClient? llm = null) : IQylLoomChatClientBuilder
{
    /// <inheritdoc />
    public IChatClient? BuildChatClient() => llm;
}
