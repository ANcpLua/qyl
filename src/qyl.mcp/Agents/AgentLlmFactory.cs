using ANcpLua.Agents.Factory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace qyl.mcp.Agents;

/// <summary>
///     qyl-flavored entry point that preserves the <c>QYL_AGENT_API_KEY</c> /
///     <c>QYL_AGENT_MODEL</c> / <c>QYL_AGENT_ENDPOINT</c> configuration keys.
///     Delegates to <see cref="AgentChatClientFactory" /> in ANcpLua.Agents.Factory
///     and wraps the result with <c>WithQylTelemetry()</c> so every tool-consumer
///     inherits OTel GenAI semconv 1.40 + qyl tool-execution spans without having
///     to compose the middleware themselves.
/// </summary>
internal static class AgentLlmFactory
{
    public static IChatClient? TryCreate(IConfiguration config) =>
        AgentChatClientFactory.TryCreate(new AgentChatClientOptions(
            config["QYL_AGENT_API_KEY"],
            config["QYL_AGENT_MODEL"],
            config["QYL_AGENT_ENDPOINT"]))?.WithQylTelemetry();
}
