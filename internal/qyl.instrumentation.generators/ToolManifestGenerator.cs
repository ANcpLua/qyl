// Copyright (c) 2025-2026 ancplua

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Qyl.Instrumentation.Generators.CallSites;
using Qyl.Instrumentation.Generators.Emitters;

namespace Qyl.Instrumentation.Generators;

/// <summary>
///     Per-concern generator that emits <c>QylToolManifest.g.cs</c> — a compile-time
///     <c>Type[]</c> listing every <c>[McpServerToolType]</c>-decorated class in the
///     current compilation. Replaces the hand-maintained array in the runtime
///     <c>McpToolRegistry</c>.
/// </summary>
/// <remarks>
///     Not gated by MSBuild toggles or the Qyl runtime reference: if the MCP SDK attribute
///     isn't imported, <c>ForAttributeWithMetadataName</c> yields nothing and the generator
///     is a no-op for that consumer.
/// </remarks>
[Generator]
public sealed class ToolManifestGenerator : IIncrementalGenerator
{
    private const string GeneratedFileName = "QylToolManifest.g.cs";
    private const string PipelineStage = nameof(ToolManifestGenerator) + ".Types";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var toolTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ToolManifestAnalyzer.McpServerToolTypeMetadataName,
                ToolManifestAnalyzer.CouldBeToolTypeClass,
                ToolManifestAnalyzer.ExtractToolType)
            .WhereNotNull()
            .WithTrackingName(PipelineStage);

        context.RegisterSourceOutput(
            toolTypes.CollectAsEquatableArray(),
            static (spc, types) =>
            {
                if (types.IsDefaultOrEmpty) return;
                var source = ToolManifestEmitter.Emit(types.AsImmutableArray());
                if (!string.IsNullOrEmpty(source))
                    spc.AddSource(GeneratedFileName, SourceText.From(source, Encoding.UTF8));
            });
    }
}
