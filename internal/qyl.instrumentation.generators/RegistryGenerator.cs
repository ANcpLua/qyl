
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

        var hostedServices = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                HostedServiceAnalyzer.HostedServiceAttributeMetadataName,
                HostedServiceAnalyzer.CouldBeHostedServiceClass,
                HostedServiceAnalyzer.Extract)
            .WhereNotNull()
            .WithTrackingName(nameof(RegistryGenerator) + ".HostedServices");

        var mapEndpoints = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MapEndpointsAnalyzer.MapEndpointsAttributeMetadataName,
                MapEndpointsAnalyzer.CouldBeMapEndpointsMethod,
                MapEndpointsAnalyzer.Extract)
            .WhereNotNull()
            .WithTrackingName(nameof(RegistryGenerator) + ".MapEndpoints");

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

        var combined = hostedServices.CollectAsEquatableArray()
            .Combine(mapEndpoints.CollectAsEquatableArray())
            .Combine(services.CollectAsEquatableArray())
            .Combine(healthChecks.CollectAsEquatableArray())
            .Combine(runtimeAvailable);

        context.RegisterSourceOutput(
            combined,
            static (spc, input) =>
            {
                var ((((hosted, endpoints), svcs), health), runtime) = input;
                if (!runtime) return;

                var source = HostedServiceEmitter.Emit(
                    hosted.IsDefaultOrEmpty ? [] : hosted.AsImmutableArray(),
                    endpoints.IsDefaultOrEmpty ? [] : endpoints.AsImmutableArray(),
                    svcs.IsDefaultOrEmpty ? [] : svcs.AsImmutableArray(),
                    health.IsDefaultOrEmpty ? [] : health.AsImmutableArray());

                spc.AddSource(GeneratedFileName, SourceText.From(source, Encoding.UTF8));
            });
    }
}
