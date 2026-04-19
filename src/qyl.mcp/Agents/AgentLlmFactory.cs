using ANcpLua.Agents.Factory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace qyl.mcp.Agents;

/// <summary>
///     qyl-flavored entry point that preserves the <c>QYL_AGENT_API_KEY</c> /
///     <c>QYL_AGENT_MODEL</c> / <c>QYL_AGENT_ENDPOINT</c> configuration keys.
///     Delegates to <see cref="AgentChatClientFactory"/> in ANcpLua.Agents.Factory.
/// </summary>
internal static class AgentLlmFactory
{
    public static IChatClient? TryCreate(IConfiguration config) =>
        AgentChatClientFactory.TryCreate(new AgentChatClientOptions(
            ApiKey: config["QYL_AGENT_API_KEY"],
            Model: config["QYL_AGENT_MODEL"],
            Endpoint: config["QYL_AGENT_ENDPOINT"]));
}
