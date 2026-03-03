namespace qyl.mcp.Skills;

/// <summary>
///     Reads QYL_SKILLS environment variable to determine which tool skills are enabled.
///     Default: all skills enabled. Format: "inspect,health,build" or "all".
/// </summary>
public sealed class SkillConfiguration
{
    private readonly HashSet<QylSkillKind> _enabled;
    private readonly bool _all;

    private SkillConfiguration(HashSet<QylSkillKind> enabled, bool all)
    {
        _enabled = enabled;
        _all = all;
    }

    public bool IsEnabled(QylSkillKind skill) => _all || _enabled.Contains(skill);

    public static SkillConfiguration FromEnvironment()
    {
        string? raw = Environment.GetEnvironmentVariable("QYL_SKILLS");

        if (string.IsNullOrWhiteSpace(raw) || raw.EqualsIgnoreCase("all"))
            return new SkillConfiguration([], all: true);

        HashSet<QylSkillKind> enabled = [];
        foreach (string part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<QylSkillKind>(part, ignoreCase: true, out QylSkillKind kind))
                enabled.Add(kind);
        }

        // If no valid skills parsed, default to all
        return new SkillConfiguration(enabled, enabled.Count == 0);
    }
}
