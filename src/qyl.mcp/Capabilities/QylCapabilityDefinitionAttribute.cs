namespace qyl.mcp.Capabilities;

/// <summary>
///     Declares a capability definition on a marker class. The generator joins this with
///     <see cref="QylCapabilityAttribute" /> occurrences on tool methods to produce
///     <c>QylToolManifest.Capabilities[]</c>. Tool references are resolved against
///     <c>[McpServerTool(Name = ...)]</c> at compile time — dangling references become
///     generator diagnostics instead of silent runtime holes.
/// </summary>
/// <remarks>
///     Two constructors: use the single-arg form for "core" capabilities that are always enabled
///     (e.g. server introspection). Use the two-arg form when the capability is gated on a skill
///     family via <see cref="SkillConfiguration.IsEnabled" />.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class QylCapabilityDefinitionAttribute : Attribute
{
    public QylCapabilityDefinitionAttribute(string id)
    {
        Id = id;
    }

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
