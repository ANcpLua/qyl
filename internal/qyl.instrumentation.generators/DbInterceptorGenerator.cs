// Copyright (c) 2025-2026 ancplua

using Microsoft.CodeAnalysis;
using Qyl.Instrumentation.Generators.CallSites;
using Qyl.Instrumentation.Generators.Emitters;

namespace Qyl.Instrumentation.Generators;

/// <summary>
///     Per-concern generator for ADO.NET <c>DbCommand</c> call-site interception.
///     Emits <c>DbIntercepts.g.cs</c> containing an <c>InterceptsLocationAttribute</c>
///     for every <c>ExecuteReader</c>/<c>ExecuteNonQuery</c>/<c>ExecuteScalar</c> site
///     (sync + async variants) that routes through <c>Qyl.Instrumentation.Instrumentation.Db</c>.
/// </summary>
/// <remarks>
///     Gated by the <c>QylDatabase</c> MSBuild property (default <c>true</c>) AND by the
///     presence of the Qyl.Instrumentation runtime in the consumer's reference graph.
/// </remarks>
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
