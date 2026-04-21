using ANcpLua.Agents.Governance;

namespace qyl.mcp.Agents;

/// <summary>
///     qyl-flavored entry point that preserves the <c>QYL_AGENT_MAX_TOOL_CALLS</c>
///     environment variable. Delegates to <see cref="AgentCallGuard" /> in
///     ANcpLua.Agents.Governance.
/// </summary>
internal static class InvestigationGuard
{
    public static AgentCallGuard FromEnvironment(int defaultMax = 200)
    {
        var raw = Environment.GetEnvironmentVariable("QYL_AGENT_MAX_TOOL_CALLS");
        var max = raw is not null && int.TryParse(raw, out var parsed) && parsed > 0
            ? parsed
            : defaultMax;
        return new AgentCallGuard(max);
    }
}
