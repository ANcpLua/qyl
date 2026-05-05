
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Qyl.Instrumentation.Generators.CallSites;
using Qyl.Instrumentation.Generators.Emitters;

namespace Qyl.Instrumentation.Generators;

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
