using System.Collections.Immutable;
using System.Text;
using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Qyl.ServiceDefaults.Generator.Analyzers;
using Qyl.ServiceDefaults.Generator.Emitters;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator;

/// <summary>
///     Intercepts WebApplicationBuilder.Build() calls to auto-register Qyl service defaults.
/// </summary>
/// <remarks>
///     See: https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md
/// </remarks>
[Generator]
public sealed class ServiceDefaultsSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // =====================================================================
        // BUILDER INTERCEPTION PIPELINE
        // Discovers WebApplicationBuilder.Build() calls and intercepts them
        // to inject Qyl service defaults and endpoint registration.
        // =====================================================================

        // Step 1: Detect if Qyl runtime is available (prerequisite gate)
        var qylRuntimeAvailable = context.CompilationProvider
            .Select(IsQylRuntimeReferenced)
            .WithTrackingName(PipelineStage.QylRuntimeCheck);

        // Step 2: Discover Build() call sites eligible for interception
        var builderCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                CouldBeInvocation, // Fast syntactic pre-filter
                ExtractBuilderCallSite) // Semantic analysis
            .WhereNotNull()
            .WithTrackingName(PipelineStage.BuilderCallSitesDiscovered)
            .CollectAsEquatableArray()
            .WithTrackingName(PipelineStage.BuilderCallSitesCollected);

        // Step 3: Emit interceptor code when prerequisites are met
        context.RegisterSourceOutput(
            builderCallSites.CombineWith(qylRuntimeAvailable),
            EmitBuilderInterceptors);

        // =====================================================================
        // GENAI SDK INTERCEPTION PIPELINE
        // Discovers GenAI SDK calls and wraps them with OTel telemetry.
        // =====================================================================

        var genAiRuntimeAvailable = context.CompilationProvider
            .Select(IsGenAiRuntimeReferenced)
            .WithTrackingName(PipelineStage.GenAiRuntimeCheck);

        var genAiCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                GenAiCallSiteAnalyzer.CouldBeGenAiInvocation,
                GenAiCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.GenAiCallSitesDiscovered)
            .CollectAsEquatableArray()
            .WithTrackingName(PipelineStage.GenAiCallSitesCollected);

        context.RegisterSourceOutput(
            genAiCallSites.CombineWith(genAiRuntimeAvailable),
            EmitGenAiInterceptors);

        // =====================================================================
        // DATABASE INTERCEPTION PIPELINE
        // Discovers DbCommand calls and wraps them with database telemetry.
        // =====================================================================

        var dbCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                DbCallSiteAnalyzer.CouldBeDbInvocation,
                DbCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.DbCallSitesDiscovered)
            .CollectAsEquatableArray()
            .WithTrackingName(PipelineStage.DbCallSitesCollected);

        context.RegisterSourceOutput(
            dbCallSites.CombineWith(qylRuntimeAvailable),
            EmitDbInterceptors);

        // =====================================================================
        // OTEL TAG BINDING PIPELINE
        // Discovers [OTel] attributes and generates tag extraction helpers.
        // =====================================================================

        var otelTagBindings = context.SyntaxProvider
            .CreateSyntaxProvider(
                OTelTagAnalyzer.CouldHaveOTelAttribute,
                OTelTagAnalyzer.ExtractTagBinding)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.OTelTagBindingsDiscovered)
            .CollectAsEquatableArray()
            .WithTrackingName(PipelineStage.OTelTagBindingsCollected);

        context.RegisterSourceOutput(
            otelTagBindings.CombineWith(qylRuntimeAvailable),
            EmitOTelTagExtensions);

        // =====================================================================
        // METER DEFINITION PIPELINE
        // Discovers [Meter] classes and generates metric implementations.
        // =====================================================================

        var meterDefinitions = context.SyntaxProvider
            .CreateSyntaxProvider(
                MeterAnalyzer.CouldBeMeterClass,
                MeterAnalyzer.ExtractDefinition)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.MeterDefinitionsDiscovered)
            .CollectAsEquatableArray()
            .WithTrackingName(PipelineStage.MeterDefinitionsCollected);

        context.RegisterSourceOutput(
            meterDefinitions.CombineWith(qylRuntimeAvailable),
            EmitMeterImplementations);

        // =====================================================================
        // TRACED METHOD PIPELINE
        // Discovers [Traced] methods and generates span interceptors.
        // =====================================================================

        var tracedCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                TracedCallSiteAnalyzer.CouldBeTracedInvocation,
                TracedCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.TracedCallSitesDiscovered)
            .CollectAsEquatableArray()
            .WithTrackingName(PipelineStage.TracedCallSitesCollected);

        context.RegisterSourceOutput(
            tracedCallSites.CombineWith(qylRuntimeAvailable),
            EmitTracedInterceptors);
    }

    // =========================================================================
    // RUNTIME AVAILABILITY CHECKS
    // These gate code generation based on whether required libraries are referenced.
    // =========================================================================

    /// <summary>
    ///     Checks if the Qyl.ServiceDefaults runtime is referenced.
    ///     This is the prerequisite for most codegen operations.
    /// </summary>
    private static bool IsQylRuntimeReferenced(Compilation compilation, CancellationToken _)
    {
        return compilation.GetTypeByMetadataName(WellKnownType.QylServiceDefaults) is not null;
    }

    /// <summary>
    ///     Checks if the GenAI instrumentation runtime is referenced.
    ///     GenAI interception can work independently of full service defaults.
    /// </summary>
    private static bool IsGenAiRuntimeReferenced(Compilation compilation, CancellationToken _)
    {
        return compilation.GetTypeByMetadataName(WellKnownType.GenAiInstrumentation) is not null;
    }

    // =========================================================================
    // SYNTACTIC PRE-FILTER
    // Fast check that runs on every syntax node. Must be cheap - no semantic model!
    // =========================================================================

    /// <summary>
    ///     Fast syntactic check: could this node be a method invocation?
    ///     This casts a wide net; semantic analysis narrows it down.
    /// </summary>
    private static bool CouldBeInvocation(SyntaxNode node, CancellationToken _)
    {
        return node.IsKind(SyntaxKind.InvocationExpression);
    }

    // =========================================================================
    // BUILDER CALL SITE EXTRACTION
    // Semantic analysis to identify and validate WebApplicationBuilder.Build() calls.
    // =========================================================================

    /// <summary>
    ///     Analyzes an invocation to determine if it's a WebApplicationBuilder.Build()
    ///     call that should be intercepted.
    /// </summary>
    /// <returns>A validated call site descriptor, or null if not applicable.</returns>
    private static BuilderCallSite? ExtractBuilderCallSite(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (!AnalyzerHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        if (!IsWebApplicationBuilderBuildCall(invocation, context.SemanticModel.Compilation))
            return null;

        // Avoid conflicts: skip if another generator has already intercepted this call
        if (AnalyzerHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        var location = ExtractInterceptableLocation(context, cancellationToken);
        return location is null
            ? null
            : new BuilderCallSite(
                AnalyzerHelpers.FormatSortKey(context.Node),
                BuilderCallKind.Build,
                location);
    }


    private static bool IsWebApplicationBuilderBuildCall(
        IInvocationOperation invocation,
        Compilation compilation)
    {
        if (invocation.TargetMethod.Name != MethodName.Build)
            return false;

        var webAppBuilderType = compilation.GetTypeByMetadataName(WellKnownType.WebApplicationBuilder);
        return SymbolEqualityComparer.Default.Equals(
            invocation.TargetMethod.ContainingType,
            webAppBuilderType);
    }


    private static InterceptableLocation? ExtractInterceptableLocation(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        return context.SemanticModel.GetInterceptableLocation(
            (InvocationExpressionSyntax)context.Node,
            cancellationToken);
    }


    // =========================================================================
    // CODE EMITTERS
    // Transform discovered call sites into generated source code.
    // =========================================================================

    private static void EmitBuilderInterceptors(
        SourceProductionContext context,
        (EquatableArray<BuilderCallSite> CallSites, bool QylRuntimeAvailable) input)
    {
        if (!input.QylRuntimeAvailable)
            return;

        var sourceCode = GenerateBuilderInterceptorSource(input.CallSites.AsImmutableArray());
        context.AddSource(GeneratedFile.BuilderInterceptors, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static void EmitGenAiInterceptors(
        SourceProductionContext context,
        (EquatableArray<GenAiCallSite> CallSites, bool GenAiRuntimeAvailable) input)
    {
        if (!input.GenAiRuntimeAvailable || input.CallSites.IsEmpty)
            return;

        var sourceCode = GenAiInterceptorEmitter.Emit(input.CallSites.AsImmutableArray());
        if (!string.IsNullOrEmpty(sourceCode))
            context.AddSource(GeneratedFile.GenAiInterceptors, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static void EmitDbInterceptors(
        SourceProductionContext context,
        (EquatableArray<DbCallSite> CallSites, bool QylRuntimeAvailable) input)
    {
        if (!input.QylRuntimeAvailable || input.CallSites.IsEmpty)
            return;

        var sourceCode = DbInterceptorEmitter.Emit(input.CallSites.AsImmutableArray());
        if (!string.IsNullOrEmpty(sourceCode))
            context.AddSource(GeneratedFile.DbInterceptors, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static void EmitOTelTagExtensions(
        SourceProductionContext context,
        (EquatableArray<OTelTagBinding> Bindings, bool QylRuntimeAvailable) input)
    {
        if (!input.QylRuntimeAvailable || input.Bindings.IsEmpty)
            return;

        var sourceCode = OTelTagsEmitter.Emit(input.Bindings.AsImmutableArray());
        if (!string.IsNullOrEmpty(sourceCode))
            context.AddSource(GeneratedFile.OTelTagExtensions, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static void EmitMeterImplementations(
        SourceProductionContext context,
        (EquatableArray<MeterDefinition> Definitions, bool QylRuntimeAvailable) input)
    {
        if (!input.QylRuntimeAvailable || input.Definitions.IsEmpty)
            return;

        var sourceCode = MeterEmitter.Emit(input.Definitions.AsImmutableArray());
        if (!string.IsNullOrEmpty(sourceCode))
            context.AddSource(GeneratedFile.MeterImplementations, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static void EmitTracedInterceptors(
        SourceProductionContext context,
        (EquatableArray<TracedCallSite> CallSites, bool QylRuntimeAvailable) input)
    {
        if (!input.QylRuntimeAvailable || input.CallSites.IsEmpty)
            return;

        var sourceCode = TracedInterceptorEmitter.Emit(input.CallSites.AsImmutableArray());
        if (!string.IsNullOrEmpty(sourceCode))
            context.AddSource(GeneratedFile.TracedInterceptors, SourceText.From(sourceCode, Encoding.UTF8));
    }

    // =========================================================================
    // SOURCE CODE GENERATION
    // =========================================================================

    private static string GenerateBuilderInterceptorSource(ImmutableArray<BuilderCallSite> callSites)
    {
        var sb = new StringBuilder();

        sb.AppendLine(CodeTemplate.AutoGeneratedHeader);
        sb.AppendLine(CodeTemplate.PragmaDisableAll);
        sb.AppendLine();
        sb.AppendLine(CodeTemplate.InterceptsLocationAttribute);
        sb.AppendLine(CodeTemplate.InterceptorsNamespaceOpen);

        var orderedCallSites = callSites.OrderBy(static c => c.SortKey, StringComparer.Ordinal);
        var index = 0;
        foreach (var callSite in orderedCallSites)
        {
            if (callSite.Kind is BuilderCallKind.Build)
                AppendBuildInterceptorMethod(sb, callSite, index);
            index++;
        }

        sb.AppendLine(CodeTemplate.InterceptorsNamespaceClose);
        return sb.ToString();
    }

    private static void AppendBuildInterceptorMethod(
        StringBuilder sb,
        BuilderCallSite callSite,
        int index)
    {
        var displayLocation = callSite.Location.GetDisplayLocation();
        var interceptAttribute = callSite.Location.GetInterceptsLocationAttributeSyntax();

        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static global::{{WellKnownType.WebApplication}} {{MethodName.InterceptBuildPrefix}}{{index}}(
                                    this global::{{WellKnownType.WebApplicationBuilder}} builder)
                                {
                                    builder.{{MethodName.TryUseQylConventions}}();
                                    var app = builder.{{MethodName.Build}}();
                                    app.{{MethodName.MapQylDefaultEndpoints}}();
                                    return app;
                                }
                        """);
    }

    // =========================================================================
    // CONSTANTS - Organized by semantic category
    // =========================================================================

    /// <summary>
    ///     Fully-qualified type names for semantic analysis and code generation.
    /// </summary>
    private static class WellKnownType
    {
        // ASP.NET Core types we intercept
        public const string WebApplicationBuilder = "Microsoft.AspNetCore.Builder.WebApplicationBuilder";
        public const string WebApplication = "Microsoft.AspNetCore.Builder.WebApplication";

        // Qyl runtime types that enable codegen
        public const string QylServiceDefaults = "Qyl.ServiceDefaults.AspNetCore.ServiceDefaults.QylServiceDefaults";
        public const string GenAiInstrumentation = "Qyl.ServiceDefaults.Instrumentation.GenAi.GenAiInstrumentation";
    }

    /// <summary>
    ///     Method names used in interception detection and code generation.
    /// </summary>
    private static class MethodName
    {
        // Methods we intercept
        public const string Build = "Build";

        // Methods injected into generated code
        public const string TryUseQylConventions = "TryUseQylConventions";
        public const string MapQylDefaultEndpoints = "MapQylDefaultEndpoints";
        public const string InterceptBuildPrefix = "Intercept_Build";
    }

    /// <summary>
    ///     Pipeline stage names for incremental generator debugging.
    ///     These appear in Roslyn's generator driver diagnostics.
    /// </summary>
    private static class PipelineStage
    {
        // Runtime availability checks
        public const string QylRuntimeCheck = nameof(QylRuntimeCheck);
        public const string GenAiRuntimeCheck = nameof(GenAiRuntimeCheck);

        // Builder interception pipeline
        public const string BuilderCallSitesDiscovered = nameof(BuilderCallSitesDiscovered);
        public const string BuilderCallSitesCollected = nameof(BuilderCallSitesCollected);

        // GenAI interception pipeline
        public const string GenAiCallSitesDiscovered = nameof(GenAiCallSitesDiscovered);
        public const string GenAiCallSitesCollected = nameof(GenAiCallSitesCollected);

        // Database interception pipeline
        public const string DbCallSitesDiscovered = nameof(DbCallSitesDiscovered);
        public const string DbCallSitesCollected = nameof(DbCallSitesCollected);

        // OTel tag binding pipeline
        public const string OTelTagBindingsDiscovered = nameof(OTelTagBindingsDiscovered);
        public const string OTelTagBindingsCollected = nameof(OTelTagBindingsCollected);

        // Meter definition pipeline
        public const string MeterDefinitionsDiscovered = nameof(MeterDefinitionsDiscovered);
        public const string MeterDefinitionsCollected = nameof(MeterDefinitionsCollected);

        // Traced method pipeline
        public const string TracedCallSitesDiscovered = nameof(TracedCallSitesDiscovered);
        public const string TracedCallSitesCollected = nameof(TracedCallSitesCollected);
    }

    /// <summary>
    ///     Output file names for generated source files.
    /// </summary>
    private static class GeneratedFile
    {
        public const string BuilderInterceptors = "Intercepts.g.cs";
        public const string GenAiInterceptors = "GenAiIntercepts.g.cs";
        public const string DbInterceptors = "DbIntercepts.g.cs";
        public const string OTelTagExtensions = "OTelTagExtensions.g.cs";
        public const string MeterImplementations = "MeterImplementations.g.cs";
        public const string TracedInterceptors = "TracedIntercepts.g.cs";
    }

    /// <summary>
    ///     Source code templates for generated output.
    /// </summary>
    private static class CodeTemplate
    {
        public const string AutoGeneratedHeader = "// <auto-generated/>";
        public const string PragmaDisableAll = "#pragma warning disable";

        public const string InterceptsLocationAttribute = """
                                                          namespace System.Runtime.CompilerServices
                                                          {
                                                              [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                                                              file sealed class InterceptsLocationAttribute(int version, string data) : global::System.Attribute;
                                                          }
                                                          """;

        public const string InterceptorsNamespaceOpen = """
                                                        namespace Qyl.ServiceDefaults.Generator
                                                        {
                                                            using Qyl.ServiceDefaults.AspNetCore.ServiceDefaults;

                                                            file static partial class Interceptors
                                                            {
                                                        """;

        public const string InterceptorsNamespaceClose = """
                                                             }
                                                         }
                                                         """;
    }
}
