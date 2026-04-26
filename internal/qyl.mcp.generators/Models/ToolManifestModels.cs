namespace qyl.mcp.generators.Models;

/// <summary>
///     One <c>[QylCapability("id", role)]</c> attribution on a tool method.
/// </summary>
internal sealed record CapabilityAttribution(string CapabilityId, CapabilityRoleKind Role);

internal enum CapabilityRoleKind
{
    Starting,
    FollowUp
}

/// <summary>
///     A discovered <c>[McpServerTool]</c> method on a tool type class.
/// </summary>
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

/// <summary>
///     A discovered <c>[McpServerToolType]</c>-decorated class with its tool methods,
///     skill attribution, and capability attributions aggregated across its methods.
/// </summary>
internal sealed record ToolTypeEntry(
    string FullyQualifiedTypeName,
    string? SkillKindName,
    EquatableArray<ToolMethodEntry> Methods);

/// <summary>
///     One <c>[QylCapabilityDefinition]</c> marker discovered in the compilation.
///     The generator joins these with <see cref="ToolMethodEntry.Capabilities" /> to
///     produce the runtime capability catalog.
/// </summary>
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
