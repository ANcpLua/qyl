
using Microsoft.CodeAnalysis;
using Qyl.Instrumentation.Generators.CallSites;
using Qyl.Instrumentation.Generators.Emitters;

namespace Qyl.Instrumentation.Generators;

[Generator]
public sealed class DbInterceptorGenerator : IIncrementalGenerator
{
    private const string GeneratedFileName = "DbIntercepts.g.cs";
    private const string DiagnosticId = "QSG002";
    private const string PipelineStage = nameof(DbInterceptorGenerator) + ".CallSites";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var runtimeAvailable = context.CompilationProvider
            .Select(GeneratorPipelineHelpers.IsQylRuntimeReferenced);

        var toggleEnabled = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => GeneratorPipelineHelpers.IsPipelineEnabled(options, "QylDatabase"));

        var enabled = runtimeAvailable.Combine(toggleEnabled)
            .Select(static (pair, _) => pair.Left && pair.Right);

        var callSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                DbCallSiteAnalyzer.CouldBeDbInvocation,
                DbCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage);

        GeneratorPipelineHelpers.RegisterCollectedEmitterPipeline(
            context, callSites, enabled,
            DbInterceptorEmitter.Emit, GeneratedFileName, DiagnosticId);
    }
}
