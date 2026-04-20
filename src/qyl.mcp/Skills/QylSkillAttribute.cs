namespace qyl.mcp.Skills;

/// <summary>
///     Declares the skill family that an MCP tool type belongs to.
///     Compile-time source of truth consumed by the ToolManifestGenerator to emit
///     skill-aware MCP registration, DI registration, and metadata. When absent,
///     the tool is excluded from generated registration and must be wired manually.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class QylSkillAttribute(QylSkillKind skill) : Attribute
{
    public QylSkillKind Skill { get; } = skill;
}
