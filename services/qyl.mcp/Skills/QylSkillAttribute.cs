namespace qyl.mcp.Skills;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class QylSkillAttribute(QylSkillKind skill) : Attribute
{
    public QylSkillKind Skill { get; } = skill;
}
