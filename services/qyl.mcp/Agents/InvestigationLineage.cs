using ANcpLua.Agents.Governance;
using ANcpLua.Roslyn.Utilities.Text;

namespace qyl.mcp.Agents;

internal static class InvestigationLineage
{
    public static AgentCallLineageResult TryEnter() =>
        AgentCallLineage.TryEnter(
            EnvConfig.ReadInt("QYL_AGENT_MAX_DEPTH", 3, 1),
            EnvConfig.ReadInt("QYL_AGENT_MAX_SPAWNS", 10, 1));
}
