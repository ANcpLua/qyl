using ANcpLua.Agents.Governance;

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
            ParseEnv("QYL_AGENT_MAX_DEPTH", 3),
            ParseEnv("QYL_AGENT_MAX_SPAWNS", 10));

    private static int ParseEnv(string name, int defaultValue) =>
        Environment.GetEnvironmentVariable(name) is { } raw
        && int.TryParse(raw, out var parsed)
        && parsed > 0
            ? parsed
            : defaultValue;
}
