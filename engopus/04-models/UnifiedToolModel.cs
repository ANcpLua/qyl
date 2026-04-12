// =============================================================================
// eng/generators/shared/UnifiedToolModel.cs
// =============================================================================
// Intended location: eng/generators/shared/UnifiedToolModel.cs
// Included via: <Compile Include="$(RepoRoot)eng/generators/shared/**/*.cs" />
//               in each generator .csproj (netstandard2.0 source sharing)
//
// ROOT CAUSE this file addresses:
//   Three generators model "an MCP tool method" with incompatible shapes because
//   there is no shared source inclusion mechanism across the four generator projects.
//   Each generator reinvented the model independently.
//
// Five Whys:
//   1. Three tool models diverge -> silent schema fidelity loss
//   2. No shared dependency between generators -> each defines its own
//   3. Roslyn generators are netstandard2.0 -> can't ProjectReference each other
//   4. Nobody introduced <Compile Include> sharing -> models drifted
//   5. ROOT: no eng/generators/shared/ directory with shared source files
//
// This file IS the fix for the root cause. It replaces:
//   - qyl.mcp.generators/Models/ToolManifestModels.cs (ToolMethodEntry)
//   - Qyl.Agents.Generator/Models/ToolModel.cs + ToolParameterModel.cs
//   - qyl.instrumentation.generators/Models/Models.cs (ToolTypeEntry — retire)
//   - Qyl.Agents.Generator/Models/TypeDeclarationModel.cs
//   - qyl.instrumentation.generators/Loom/Models/LoomModels.cs (LoomTypeDeclarationModel — dedupe)
//
// After adoption, each generator .csproj adds:
//   <ItemGroup>
//     <Compile Include="$(RepoRoot)eng\generators\shared\**\*.cs"
//              LinkBase="Shared" Visible="false" />
//   </ItemGroup>
// =============================================================================

#nullable enable

namespace Qyl.Generators.Shared;

/// <summary>
///     Tri-state hint: distinguishes "not specified" from "explicitly false."
///     Replaces bool in qyl.mcp.generators (which loses the Unset signal)
///     and ToolHintValue in Qyl.Agents.Generator (which already has this).
/// </summary>
internal enum ToolHintValue : byte { Unset = 0, True = 1, False = 2 }

/// <summary>
///     Task lifecycle support for long-running tools.
///     Moves from Qyl.Agents.Generator into shared — MCP generator needs this
///     for Streamable HTTP task polling.
/// </summary>
internal enum ToolTaskSupportValue : byte { Unset = 0, Forbidden = 1, Optional = 2, Required = 3 }

/// <summary>
///     Return type classification for correct async dispatch codegen.
///     Moves from Qyl.Agents.Generator into shared — MCP manifest emitter
///     needs this to generate AOT-safe CreateTools with correct awaiting.
/// </summary>
internal enum ReturnKind : byte
{
    Void,
    Sync,
    Task,
    ValueTask,
    TaskOfT,
    ValueTaskOfT
}

/// <summary>
///     Capability role on a tool method (qyl-specific extension point).
///     Stays qyl-specific — Qyl.Agents.Generator ignores it, MCP generator uses it.
/// </summary>
internal enum CapabilityRoleKind { Starting, FollowUp }

/// <summary>
///     One [QylCapability("id", role)] attribution on a tool method.
///     qyl-specific — generators that don't need capabilities pass an empty array.
/// </summary>
internal readonly record struct CapabilityAttribution(string CapabilityId, CapabilityRoleKind Role);

/// <summary>
///     A tool parameter with full JSON Schema metadata.
///     Moves from Qyl.Agents.Generator.Models.ToolParameterModel into shared.
///     The MCP generator currently skips parameter extraction — after sharing
///     this model, its analyzer can populate it and its emitter can use it
///     for richer ToolDescriptors.
/// </summary>
internal readonly record struct ToolParameterModel(
    string Name,
    string CamelCaseName,
    string TypeFullyQualified,
    string JsonSchemaType,
    string? JsonSchemaFormat,
    string? Description,
    bool IsNullable,
    bool IsRequired,
    bool IsValueType,
    string? DefaultValueLiteral,
    EquatableArray<string> EnumValues);

/// <summary>
///     Type declaration chain for nested partial class emission.
///     Deduplicates:
///       - Qyl.Agents.Generator.Models.TypeDeclarationModel
///       - Qyl.Instrumentation.Generators.Loom.Models.LoomTypeDeclarationModel
///     Identical shape, separate types — now one type.
/// </summary>
internal readonly record struct TypeDeclarationModel(
    string Name,
    string Keyword,
    string Modifiers,
    string TypeParameters,
    EquatableArray<string> ConstraintClauses);

/// <summary>
///     The unified tool model. One struct, all generators.
///
///     Subsumes ToolMethodEntry (MCP) + ToolModel (Agents).
///     ToolTypeEntry (Instrumentation) is retired — this replaces it.
/// </summary>
internal readonly record struct ToolModel(
    // Identity
    string MethodName,
    string ToolName,
    string? Title,
    string Description,

    // Return type
    ReturnKind ReturnKind,
    string ResultTypeFullyQualified,
    bool HasCancellationToken,

    // Parameters
    EquatableArray<ToolParameterModel> Parameters,

    // Tool annotations (tri-state)
    ToolHintValue ReadOnly,
    ToolHintValue Destructive,
    ToolHintValue Idempotent,
    ToolHintValue OpenWorld,
    ToolTaskSupportValue TaskSupport,

    // qyl-specific extensions (empty for Qyl.Agents.Generator)
    EquatableArray<CapabilityAttribution> Capabilities)
{
    public byte ReadOnlyHint => (byte)ReadOnly;
    public byte IdempotentHint => (byte)Idempotent;
    public byte DestructiveHint => (byte)Destructive;
    public byte OpenWorldHint => (byte)OpenWorld;
}

/// <summary>
///     A tool type class with its methods, skill, declaration chain, and capabilities.
///     Replaces both:
///       - qyl.mcp.generators.Models.ToolTypeEntry (has SkillKindName + Methods)
///       - qyl.instrumentation.generators.Models.ToolTypeEntry (vestigial: SortKey + FQN only)
/// </summary>
internal readonly record struct ToolTypeEntry(
    string FullyQualifiedTypeName,
    string? SkillKindName,
    EquatableArray<TypeDeclarationModel> DeclarationChain,
    EquatableArray<ToolModel> Methods);
