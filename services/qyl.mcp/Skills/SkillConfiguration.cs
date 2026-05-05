namespace qyl.mcp.Skills;

public sealed class SkillConfiguration
{
    private readonly bool _all;
    private readonly HashSet<QylSkillKind> _enabled;

    private SkillConfiguration(HashSet<QylSkillKind> enabled, bool all)
    {
        _enabled = enabled;
        _all = all;
    }

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

        return new SkillConfiguration(enabled, enabled.Count == 0);
    }
}
