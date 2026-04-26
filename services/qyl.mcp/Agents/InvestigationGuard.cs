using ANcpLua.Agents.Governance;
using ANcpLua.Roslyn.Utilities.Text;

namespace qyl.mcp.Agents;

/// <summary>
///     qyl-flavored entry point that preserves the <c>QYL_AGENT_MAX_TOOL_CALLS</c>
///     environment variable. Delegates to <see cref="AgentCallGuard" /> in
///     ANcpLua.Agents.Governance.
/// </summary>
internal static class InvestigationGuard
{
    public static AgentCallGuard FromEnvironment(int defaultMax = 200) =>
        new(EnvConfig.ReadInt("QYL_AGENT_MAX_TOOL_CALLS", defaultMax, 1));
}
