namespace qyl.mcp.Capabilities;

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
