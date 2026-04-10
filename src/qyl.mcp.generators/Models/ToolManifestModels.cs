using ANcpLua.Roslyn.Utilities;

namespace Qyl.Mcp.Generators.Models;

/// <summary>
///     A discovered [McpServerTool] method on a tool type class.
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
    string ReturnTypeDisplayName);

/// <summary>
///     A discovered [McpServerToolType]-decorated class with its tool methods.
/// </summary>
internal sealed record ToolTypeEntry(
    string FullyQualifiedTypeName,
    EquatableArray<ToolMethodEntry> Methods);
