using ANcpLua.Agents.Hosting.BitNet;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Qyl.Loom.Clients;

// Bridges the keyed IChatClient registered by AddQylBitNetChatClient() onto
// qyl's existing three-builder pattern. The chat client is already wrapped
// with OpenTelemetry by the hosting facade; QylLoomAgentsBuilder.Compose
// adds the second telemetry layer at the agent boundary.
internal sealed class QylLoomBitNetChatClientBuilder(
    [FromKeyedServices(QylBitNetHostingExtensions.DefaultConnectionName)] IChatClient inner)
    : IQylLoomChatClientBuilder
{
    public IChatClient? BuildChatClient() => inner;
}
