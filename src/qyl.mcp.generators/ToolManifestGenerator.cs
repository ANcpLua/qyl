using System.Text;
using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Qyl.Mcp.Generators.Analyzers;
using Qyl.Mcp.Generators.Emitters;
using Qyl.Mcp.Generators.Models;

namespace Qyl.Mcp.Generators;

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

        context.RegisterSourceOutput(toolTypeEntries, static (spc, entries) =>
        {
            var equatable = entries.AsEquatableArray();

            if (equatable.IsEmpty) return;

            var source = ToolManifestEmitter.Emit(equatable);
            if (!string.IsNullOrEmpty(source))
                spc.AddSource("QylToolManifest.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }
}
