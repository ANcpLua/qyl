using ANcpLua.Agents.Governance;
using ANcpLua.Roslyn.Utilities.Text;

namespace qyl.mcp.Agents;

internal static class InvestigationGuard
{
    public static AgentCallGuard FromEnvironment(int defaultMax = 200) =>
        new(EnvConfig.ReadInt("QYL_AGENT_MAX_TOOL_CALLS", defaultMax, 1));
}
