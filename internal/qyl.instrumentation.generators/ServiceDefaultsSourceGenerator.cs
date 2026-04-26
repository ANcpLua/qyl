// Copyright (c) 2025-2026 ancplua

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Qyl.Instrumentation.Generators.CallSites;
using Qyl.Instrumentation.Generators.Emitters;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators;

/// <summary>
///     Intercepts <c>WebApplicationBuilder.Build()</c> call sites to inject the qyl
///     service-defaults convention pair (<c>TryUseQylConventions</c> +
///     <c>MapQylDefaultEndpoints</c>) and auto-invoke the three <c>QylGeneratedRegistry</c>
///     registration methods. Also owns the <c>[Meter]</c> and <c>[Traced]</c> pipelines
///     because their discovered source names feed the builder interceptor's
///     <c>AdditionalActivitySources</c> / <c>AdditionalMeterNames</c> configuration lambda.
/// </summary>
/// <remarks>
///     <para>
///         Sibling generators handle the independent concerns:
///         <see cref="DbInterceptorGenerator" /> (ADO.NET call-sites),
///         <see cref="ToolManifestGenerator" /> (MCP tool types),
///         <see cref="RegistryGenerator" /> (hosted-service / endpoint / DI / health-check registry).
///     </para>
///     <para>
///         Runtime IChatClient + AIAgent instrumentation is NOT handled here — it lives in
///         <c>qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs</c> and is
///         composed at the composition root via <c>builder.UseQylTelemetry()</c> over
///         <c>Microsoft.Extensions.AI</c>'s <c>UseOpenTelemetry()</c> middleware (2026-04 collapse).
///     </para>
///     <para>See <see href="https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md" />.</para>
/// </remarks>
[Generator]
public sealed class ServiceDefaultsSourceGenerator : IIncrementalGenerator
{
    private const string BuilderInterceptorsFile = "Intercepts.g.cs";
    private const string MeterImplementationsFile = "MeterImplementations.g.cs";
    private const string TracedInterceptorsFile = "TracedIntercepts.g.cs";
    private const string MeterDiagnosticId = "QSG004";
    private const string TracedDiagnosticId = "QSG005";

    // =========================================================================
    // Template fragments
    // =========================================================================

    private const string InterceptsLocationAttributeDeclaration = """
                                                                  using Qyl.Instrumentation;

                                                                  namespace System.Runtime.CompilerServices
                                                                  {
                                                                      [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                                                                      file sealed class InterceptsLocationAttribute(int version, string data) : global::System.Attribute;
                                                                  }
                                                                  """;

    private const string InterceptorsNamespaceOpen = """
                                                     namespace Qyl.Instrumentation.Generators
                                                     {
                                                         file static partial class Interceptors
                                                         {
                                                     """;

    private const string InterceptorsNamespaceClose = """
                                                          }
                                                      }
                                                      """;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var runtimeAvailable = context.CompilationProvider
            .Select(GeneratorPipelineHelpers.IsQylRuntimeReferenced)
            .WithTrackingName(PipelineStage.QylRuntimeCheck);

        var toggles = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => new TelemetryToggles(
                GeneratorPipelineHelpers.IsPipelineEnabled(options, "QylTraced"),
                GeneratorPipelineHelpers.IsPipelineEnabled(options, "QylMeter")))
            .WithTrackingName(PipelineStage.ToggleCheck);

        var meterEnabled = runtimeAvailable.Combine(toggles)
            .Select(static (pair, _) => pair.Left && pair.Right.Meter);

        var tracedEnabled = runtimeAvailable.Combine(toggles)
            .Select(static (pair, _) => pair.Left && pair.Right.Traced);

        var builderCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(CouldBeBuildInvocation, ExtractBuilderCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.BuilderCallSitesDiscovered);

        var meterDefinitions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MeterAnalyzer.MeterAttributeMetadataName,
                MeterAnalyzer.CouldBeMeterClass,
                MeterAnalyzer.ExtractDefinitionFromAttribute)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.MeterDefinitionsDiscovered);

        var tracedCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                TracedCallSiteAnalyzer.CouldBeTracedInvocation,
                TracedCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.TracedCallSitesDiscovered);

        // Builder interception — combines the three inputs (call sites, runtime gate, composed
        // telemetry registration) and emits one Intercepts.g.cs covering every Build() site.
        var generatedRegistration = BuildGeneratedServiceRegistration(
            context, meterDefinitions, meterEnabled, tracedCallSites, tracedEnabled, toggles);

        var builderInput = builderCallSites.CollectAsEquatableArray()
            .Combine(runtimeAvailable)
            .Combine(generatedRegistration)
            .Select(static (input, _) => new BuilderInterceptorInput(
                input.Left.Left, input.Left.Right, input.Right));

        context.RegisterSourceOutput(builderInput, EmitBuilderInterceptors);

        // Meter + Traced emit — independent outputs, same shape as sibling generators.
        GeneratorPipelineHelpers.RegisterCollectedEmitterPipeline(
            context, meterDefinitions, meterEnabled,
            MeterEmitter.Emit, MeterImplementationsFile, MeterDiagnosticId);

        GeneratorPipelineHelpers.RegisterCollectedEmitterPipeline(
            context, tracedCallSites, tracedEnabled,
            TracedInterceptorEmitter.Emit, TracedInterceptorsFile, TracedDiagnosticId);
    }

    // =========================================================================
    // Builder call-site extraction
    // =========================================================================

    private static bool CouldBeBuildInvocation(SyntaxNode node, CancellationToken _) =>
        string.Equals(IncrementalPipelineHelpers.GetInvokedMethodName(node), "Build", StringComparison.Ordinal);

    private static BuilderCallSite? ExtractBuilderCallSite(
        GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (!IncrementalPipelineHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        if (!IsWebApplicationBuilderBuildCall(invocation, context.SemanticModel.Compilation))
            return null;

        // Another generator already intercepted this site — leave it alone.
        if (IncrementalPipelineHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        var location = context.SemanticModel.GetInterceptableLocation(
            (InvocationExpressionSyntax)context.Node, cancellationToken);

        return location is null
            ? null
            : new BuilderCallSite(
                IncrementalPipelineHelpers.FormatSortKey(context.Node),
                BuilderCallKind.Build,
                location);
    }

    private static bool IsWebApplicationBuilderBuildCall(IInvocationOperation invocation, Compilation compilation) =>
        invocation.IsMethodNamed(
            compilation.GetTypeByMetadataName(GeneratorPipelineHelpers.WebApplicationBuilderTypeName),
            "Build");

    // =========================================================================
    // Builder interceptor emission
    // =========================================================================

    private static void EmitBuilderInterceptors(SourceProductionContext context, BuilderInterceptorInput input)
    {
        if (!input.QylRuntimeAvailable)
            return;

        var source = GenerateBuilderInterceptorSource(input.CallSites.AsImmutableArray(), input.GeneratedTelemetry);
        context.AddSource(BuilderInterceptorsFile, SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateBuilderInterceptorSource(
        ImmutableArray<BuilderCallSite> callSites,
        GeneratedServiceRegistration generatedTelemetry)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine(InterceptsLocationAttributeDeclaration);
        sb.AppendLine(InterceptorsNamespaceOpen);

        var index = 0;
        foreach (var callSite in callSites.OrderBy(static c => c.SortKey, StringComparer.Ordinal))
        {
            if (callSite.Kind is BuilderCallKind.Build)
                AppendBuildInterceptorMethod(sb, callSite, index, generatedTelemetry);
            index++;
        }

        sb.AppendLine(InterceptorsNamespaceClose);
        return sb.ToString();
    }

    private static void AppendBuildInterceptorMethod(
        StringBuilder sb, BuilderCallSite callSite, int index, GeneratedServiceRegistration generatedTelemetry)
    {
        var displayLocation = callSite.Location.GetDisplayLocation();
        var interceptAttribute = callSite.Location.GetInterceptsLocationAttributeSyntax();
        var configureLambda = BuildGeneratedServiceRegistrationLambda(generatedTelemetry);
        var useQylCall = configureLambda is null
            ? "builder.TryUseQylConventions();"
            : $"builder.TryUseQylConventions({configureLambda});";

        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static global::{{GeneratorPipelineHelpers.WebApplicationTypeName}} Intercept_Build{{index}}(
                                    this global::{{GeneratorPipelineHelpers.WebApplicationBuilderTypeName}} builder)
                                {
                                    {{useQylCall}}
                                    global::Qyl.Instrumentation.Generators.QylGeneratedRegistry.RegisterQylHostedServices(builder.Services);
                                    global::Qyl.Instrumentation.Generators.QylGeneratedRegistry.RegisterQylServices(builder.Services);
                                    global::Qyl.Instrumentation.Generators.QylGeneratedRegistry.RegisterQylHealthChecks(builder.Services);
                                    var app = builder.Build();
                                    app.MapQylDefaultEndpoints();
                                    return app;
                                }
                        """);
    }

    // =========================================================================
    // Telemetry composition — merges current-assembly meter/traced names with
    // names announced by referenced assemblies via [GeneratedActivitySource] /
    // [GeneratedMeter] assembly-level attributes.
    // =========================================================================

    private static IncrementalValueProvider<GeneratedServiceRegistration> BuildGeneratedServiceRegistration(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<MeterDefinition> meterDefinitions,
        IncrementalValueProvider<bool> meterEnabled,
        IncrementalValuesProvider<TracedCallSite> tracedCallSites,
        IncrementalValueProvider<bool> tracedEnabled,
        IncrementalValueProvider<TelemetryToggles> toggles)
    {
        var currentTelemetry = meterDefinitions.CollectAsEquatableArray()
            .Combine(meterEnabled)
            .Combine(tracedCallSites.CollectAsEquatableArray().Combine(tracedEnabled))
            .Select(static (input, _) =>
            {
                var meters = input.Left.Right ? input.Left.Left : default;
                var traced = input.Right.Right ? input.Right.Left : default;
                return BuildCurrentTelemetryRegistration(meters, traced);
            })
            .WithTrackingName(PipelineStage.GeneratedTelemetryCurrentDiscovered);

        var referencedRegistration = context.CompilationProvider
            .Select(CollectReferencedServiceRegistration)
            .WithTrackingName(PipelineStage.GeneratedTelemetryReferencedDiscovered);

        return currentTelemetry
            .Combine(referencedRegistration)
            .Combine(toggles)
            .Select(static (input, _) =>
            {
                var ((telemetry, referenced), activeToggles) = input;
                return FilterServiceRegistration(
                    MergeServiceRegistrations(telemetry, referenced),
                    activeToggles);
            })
            .WithTrackingName(PipelineStage.GeneratedTelemetryCombined);
    }

    private static GeneratedServiceRegistration BuildCurrentTelemetryRegistration(
        EquatableArray<MeterDefinition> meters, EquatableArray<TracedCallSite> tracedCallSites) =>
        new(
            tracedCallSites.IsDefaultOrEmpty
                ? default
                : tracedCallSites
                    .Select(static c => c.ActivitySourceName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static n => n, StringComparer.Ordinal)
                    .ToArray()
                    .ToEquatableArray(),
            meters.IsDefaultOrEmpty
                ? default
                : meters
                    .Select(static m => m.MeterName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static n => n, StringComparer.Ordinal)
                    .ToArray()
                    .ToEquatableArray());

    private static GeneratedServiceRegistration CollectReferencedServiceRegistration(
        Compilation compilation, CancellationToken _)
    {
        var activitySources = new SortedSet<string>(StringComparer.Ordinal);
        var meterNames = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                continue;

            foreach (var attribute in assembly.GetAttributes())
            {
                var attributeName = attribute.AttributeClass?.ToDisplayString();

                if (attribute.ConstructorArguments.Length is not 1) continue;
                if (attribute.GetConstructorArgument<string>(0) is not { Length: > 0 } name) continue;

                if (string.Equals(attributeName, GeneratorPipelineHelpers.GeneratedActivitySourceAttributeName,
                        StringComparison.Ordinal))
                    activitySources.Add(name);
                else if (string.Equals(attributeName, GeneratorPipelineHelpers.GeneratedMeterAttributeName,
                             StringComparison.Ordinal))
                    meterNames.Add(name);
            }
        }

        return new GeneratedServiceRegistration(
            activitySources.Count is 0 ? default : activitySources.ToArray().ToEquatableArray(),
            meterNames.Count is 0 ? default : meterNames.ToArray().ToEquatableArray());
    }

    private static GeneratedServiceRegistration MergeServiceRegistrations(
        GeneratedServiceRegistration left, GeneratedServiceRegistration right) =>
        new(
            MergeNames(left.ActivitySources, right.ActivitySources),
            MergeNames(left.MeterNames, right.MeterNames));

    private static EquatableArray<string> MergeNames(EquatableArray<string> left, EquatableArray<string> right)
    {
        if (left.IsDefaultOrEmpty && right.IsDefaultOrEmpty)
            return default;

        return left.Concat(right)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static n => n, StringComparer.Ordinal)
            .ToArray()
            .ToEquatableArray();
    }

    private static GeneratedServiceRegistration FilterServiceRegistration(
        GeneratedServiceRegistration registration, TelemetryToggles toggles) =>
        new(
            toggles.Traced ? registration.ActivitySources : default,
            toggles.Meter ? registration.MeterNames : default);

    private static string? BuildGeneratedServiceRegistrationLambda(GeneratedServiceRegistration registration)
    {
        if (registration.ActivitySources.IsDefaultOrEmpty && registration.MeterNames.IsDefaultOrEmpty)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("static options =>");
        sb.AppendLine("        {");

        foreach (var activitySource in registration.ActivitySources)
            sb.AppendLine($"            options.AdditionalActivitySources.Add({Literal(activitySource)});");

        foreach (var meterName in registration.MeterNames)
            sb.AppendLine($"            options.AdditionalMeterNames.Add({Literal(meterName)});");

        sb.Append("        }");
        return sb.ToString();
    }

    private static string Literal(string s) => SymbolDisplay.FormatLiteral(s, true);

    private static class PipelineStage
    {
        public const string QylRuntimeCheck = nameof(QylRuntimeCheck);
        public const string BuilderCallSitesDiscovered = nameof(BuilderCallSitesDiscovered);
        public const string MeterDefinitionsDiscovered = nameof(MeterDefinitionsDiscovered);
        public const string TracedCallSitesDiscovered = nameof(TracedCallSitesDiscovered);
        public const string ToggleCheck = nameof(ToggleCheck);
        public const string GeneratedTelemetryCurrentDiscovered = nameof(GeneratedTelemetryCurrentDiscovered);
        public const string GeneratedTelemetryReferencedDiscovered = nameof(GeneratedTelemetryReferencedDiscovered);
        public const string GeneratedTelemetryCombined = nameof(GeneratedTelemetryCombined);
    }

    private readonly record struct TelemetryToggles(bool Traced, bool Meter);

    private readonly record struct GeneratedServiceRegistration(
        EquatableArray<string> ActivitySources,
        EquatableArray<string> MeterNames);

    private readonly record struct BuilderInterceptorInput(
        EquatableArray<BuilderCallSite> CallSites,
        bool QylRuntimeAvailable,
        GeneratedServiceRegistration GeneratedTelemetry);
}
