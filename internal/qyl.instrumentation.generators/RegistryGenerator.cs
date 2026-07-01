
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Qyl.Instrumentation.Generators.CallSites;
using Qyl.Instrumentation.Generators.Emitters;

namespace Qyl.Instrumentation.Generators;

[Generator]
public sealed class RegistryGenerator : IIncrementalGenerator
{
    private const string GeneratedFileName = "QylGeneratedRegistry.g.cs";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var runtimeAvailable = context.CompilationProvider
            .Select(GeneratorPipelineHelpers.IsQylRuntimeReferenced);

        var services = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                QylServiceAnalyzer.QylServiceAttributeMetadataName,
                QylServiceAnalyzer.CouldBeQylServiceClass,
                QylServiceAnalyzer.Extract)
            .WhereNotNull()
            .WithTrackingName(nameof(RegistryGenerator) + ".Services");

        var healthChecks = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                QylHealthCheckAnalyzer.QylHealthCheckAttributeMetadataName,
                QylHealthCheckAnalyzer.CouldBeHealthCheckClass,
                QylHealthCheckAnalyzer.Extract)
            .WhereNotNull()
            .WithTrackingName(nameof(RegistryGenerator) + ".HealthChecks");

        var combined = services.CollectAsEquatableArray()
            .Combine(healthChecks.CollectAsEquatableArray())
            .Combine(runtimeAvailable);

        context.RegisterSourceOutput(
            combined,
            static (spc, input) =>
            {
                var ((svcs, health), runtime) = input;
                if (!runtime) return;

                var source = QylRegistryEmitter.Emit(
                    svcs.IsDefaultOrEmpty ? [] : svcs.AsImmutableArray(),
                    health.IsDefaultOrEmpty ? [] : health.AsImmutableArray());

                spc.AddSource(GeneratedFileName, SourceText.From(source, Encoding.UTF8));
            });
    }
}
