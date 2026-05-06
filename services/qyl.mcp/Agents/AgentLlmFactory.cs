using ANcpLua.Agents.Hosting.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace qyl.mcp.Agents;

internal static class AgentLlmFactory
{
    public static IChatClient? TryCreate(IConfiguration config) =>
        AgentChatClientFactory.TryCreate(new AgentChatClientOptions(
            config["QYL_AGENT_API_KEY"],
            config["QYL_AGENT_MODEL"],
            config["QYL_AGENT_ENDPOINT"]))?.WithQylTelemetry();
}
