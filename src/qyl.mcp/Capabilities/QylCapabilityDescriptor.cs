using qyl.mcp.Skills;

namespace qyl.mcp.Capabilities;

/// <summary>
///     Runtime shape of one capability, populated by ToolManifestGenerator from
///     <see cref="QylCapabilityDefinitionAttribute" /> markers and <see cref="QylCapabilityAttribute" />
///     method attributions. Tool name lists are verified against the generated tool manifest at
///     compile time — no string drift possible.
/// </summary>
internal sealed record QylCapabilityDescriptor
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Skill { get; init; }
    public QylSkillKind? RequiredSkill { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required IReadOnlyList<string> ToolNames { get; init; }
    public required IReadOnlyList<string> UseCases { get; init; }
    public required IReadOnlyList<string> PrimaryIdentifiers { get; init; }
    public required IReadOnlyList<string> StartingTools { get; init; }
    public required IReadOnlyList<string> FollowUpTools { get; init; }
    public required IReadOnlyList<string> ScopingHints { get; init; }
    public required IReadOnlyList<string> EvidenceHints { get; init; }
    public required IReadOnlyList<string> RelatedCapabilityIds { get; init; }
}
