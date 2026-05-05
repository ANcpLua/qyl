namespace qyl.mcp.Capabilities;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class QylCapabilityDefinitionAttribute : Attribute
{
    public QylCapabilityDefinitionAttribute(string id) => Id = id;

    public QylCapabilityDefinitionAttribute(string id, QylSkillKind requiredSkill)
    {
        Id = id;
        RequiredSkill = requiredSkill;
        HasRequiredSkill = true;
    }

    public string Id { get; }
    public QylSkillKind RequiredSkill { get; }
    public bool HasRequiredSkill { get; }

    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string SkillLabel { get; init; } = "";
    public string[] Tags { get; init; } = [];
    public string[] UseCases { get; init; } = [];
    public string[] PrimaryIdentifiers { get; init; } = [];
    public string[] ScopingHints { get; init; } = [];
    public string[] EvidenceHints { get; init; } = [];
    public string[] RelatedCapabilities { get; init; } = [];
}
