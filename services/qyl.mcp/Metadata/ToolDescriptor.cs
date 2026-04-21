namespace qyl.mcp.Metadata;

/// <summary>
///     Compile-time descriptor of a single MCP tool method, emitted by ToolManifestGenerator.
///     Consolidates both the method-level metadata ([McpServerTool]/[Description]) and the
///     tool-type skill family ([QylSkill]) into one record — there is no second runtime-only
///     descriptor that has to enrich this shape.
/// </summary>
internal sealed record ToolDescriptor
{
    public required string Name { get; init; }
    public required string MethodName { get; init; }
    public required string DeclaringType { get; init; }
    public required string Skill { get; init; }
    public QylSkillKind? SkillKind { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public required bool ReadOnly { get; init; }
    public required bool Destructive { get; init; }
    public required bool Idempotent { get; init; }
    public required bool OpenWorld { get; init; }
    public required string ReturnType { get; init; }
}
