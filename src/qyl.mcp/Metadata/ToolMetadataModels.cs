namespace qyl.mcp.Metadata;

internal sealed record GeneratedToolDescriptor
{
    public required string Name { get; init; }
    public required string MethodName { get; init; }
    public required string DeclaringType { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public required bool ReadOnly { get; init; }
    public required bool Destructive { get; init; }
    public required bool Idempotent { get; init; }
    public required bool OpenWorld { get; init; }
    public required string ReturnType { get; init; }
}

internal sealed record RuntimeToolDescriptor
{
    public required string Name { get; init; }
    public required string MethodName { get; init; }
    public required string DeclaringType { get; init; }
    public required string Skill { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public required bool ReadOnly { get; init; }
    public required bool Destructive { get; init; }
    public required bool Idempotent { get; init; }
    public required bool OpenWorld { get; init; }
    public required string ReturnType { get; init; }
}
