namespace qyl.mcp.Skills;

/// <summary>
///     Skill-label helpers. Tool-to-skill mapping now lives entirely in
///     <see cref="QylSkillAttribute" /> annotations on tool classes and is surfaced via
///     the generator-produced <c>QylToolManifest.ToolDescriptors</c>.
/// </summary>
internal static class QylSkillCatalog
{
    public static IReadOnlyList<string> GetEnabledSkillLabels(SkillConfiguration skills)
    {
        List<string> labels = ["core"];
        foreach (var skill in Enum.GetValues<QylSkillKind>())
        {
            if (skills.IsEnabled(skill))
                labels.Add(SkillLabel(skill));
        }

        return labels;
    }

    public static string SkillLabel(QylSkillKind skill) =>
        skill switch
        {
            QylSkillKind.Inspect => "inspect",
            QylSkillKind.Health => "health",
            QylSkillKind.Analytics => "analytics",
            QylSkillKind.Agent => "agent",
            QylSkillKind.Build => "build",
            QylSkillKind.Anomaly => "anomaly",
            QylSkillKind.Loom => "loom",
            QylSkillKind.Apps => "apps",
            QylSkillKind.Debug => "debug",
            _ => "core"
        };
}
