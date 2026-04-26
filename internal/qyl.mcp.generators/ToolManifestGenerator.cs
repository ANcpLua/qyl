using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using qyl.mcp.generators.Analyzers;
using qyl.mcp.generators.Emitters;

namespace qyl.mcp.generators;

/// <summary>
///     Discovers [McpServerToolType] classes and their [McpServerTool] methods at compile time.
///     Emits QylToolManifest with both a Type[] array and an AOT-safe CreateTools factory.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ToolManifestGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var toolTypeEntries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ToolManifestAnalyzer.McpServerToolTypeMetadataName,
                ToolManifestAnalyzer.CouldBeToolTypeClass,
                ToolManifestAnalyzer.ExtractToolType)
            .Where(static entry => entry is not null)
            .Select(static (entry, _) => entry!)
            .Collect();

        var capabilityDefinitions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ToolManifestAnalyzer.CapabilityDefinitionMetadataName,
                ToolManifestAnalyzer.CouldBeCapabilityDefinition,
                ToolManifestAnalyzer.ExtractCapabilityDefinition)
            .Where(static entry => entry is not null)
            .Select(static (entry, _) => entry!)
            .Collect();

        var combined = toolTypeEntries.Combine(capabilityDefinitions);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var tools = pair.Left.AsEquatableArray();
            var definitions = pair.Right.AsEquatableArray();

            if (tools.IsEmpty) return;

            var source = ToolManifestEmitter.Emit(tools, definitions);
            if (!string.IsNullOrEmpty(source))
                spc.AddSource("QylToolManifest.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }
}
