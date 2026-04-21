using ANcpLua.Agents.Governance;
using ANcpLua.Roslyn.Utilities.Text;

namespace qyl.mcp.Agents;

/// <summary>
///     qyl-flavored entry point that preserves <c>QYL_AGENT_MAX_DEPTH</c> /
///     <c>QYL_AGENT_MAX_SPAWNS</c> environment variables. Delegates to
///     <see cref="AgentCallLineage" /> in ANcpLua.Agents.Governance.
/// </summary>
internal static class InvestigationLineage
{
    public static AgentCallLineageResult TryEnter() =>
        AgentCallLineage.TryEnter(
            EnvConfig.ReadInt("QYL_AGENT_MAX_DEPTH", defaultValue: 3, min: 1),
            EnvConfig.ReadInt("QYL_AGENT_MAX_SPAWNS", defaultValue: 10, min: 1));
}
