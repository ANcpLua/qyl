namespace qyl.mcp.generators.Models;

internal sealed record CapabilityAttribution(string CapabilityId, CapabilityRoleKind Role);

internal enum CapabilityRoleKind
{
    Starting,
    FollowUp
}

internal sealed record ToolMethodEntry(
    string MethodName,
    string ToolName,
    string? Title,
    string? Description,
    bool ReadOnly,
    bool Destructive,
    bool Idempotent,
    bool OpenWorld,
    string ReturnTypeDisplayName,
    EquatableArray<CapabilityAttribution> Capabilities);

internal sealed record ToolTypeEntry(
    string FullyQualifiedTypeName,
    string? SkillKindName,
    EquatableArray<ToolMethodEntry> Methods);

internal sealed record CapabilityDefinitionEntry(
    string Id,
    string Title,
    string Summary,
    string? RequiredSkillKindName,
    string SkillLabel,
    EquatableArray<string> Tags,
    EquatableArray<string> UseCases,
    EquatableArray<string> PrimaryIdentifiers,
    EquatableArray<string> ScopingHints,
    EquatableArray<string> EvidenceHints,
    EquatableArray<string> RelatedCapabilities);
