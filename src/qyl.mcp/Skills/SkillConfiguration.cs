namespace qyl.mcp.Skills;

/// <summary>
///     Reads QYL_SKILLS environment variable to determine which tool skills are enabled.
///     Default: all skills enabled. Format: "inspect,health,build" or "all".
/// </summary>
public sealed class SkillConfiguration
{
    private readonly bool _all;
    private readonly HashSet<QylSkillKind> _enabled;

    private SkillConfiguration(HashSet<QylSkillKind> enabled, bool all)
    {
        _enabled = enabled;
        _all = all;
    }

    /// <summary>
    ///     Debug skill requires explicit opt-in (QYL_SKILLS=debug,...) — never included in "all"
    ///     because it exposes IDE debugger control to MCP clients.
    /// </summary>
    public bool IsEnabled(QylSkillKind skill) =>
        skill is QylSkillKind.Debug
            ? _enabled.Contains(skill)
            : _all || _enabled.Contains(skill);

    public static SkillConfiguration FromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("QYL_SKILLS");

        if (string.IsNullOrWhiteSpace(raw) || raw.EqualsIgnoreCase("all"))
            return new SkillConfiguration([], true);

        HashSet<QylSkillKind> enabled = [];
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse(part, true, out QylSkillKind kind))
                enabled.Add(kind);
        }

        // If no valid skills parsed, default to all
        return new SkillConfiguration(enabled, enabled.Count == 0);
    }
}
