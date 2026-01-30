using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Qyl.ServiceDefaults.Generator.Analyzers;
using Qyl.ServiceDefaults.Generator.Emitters;
using Qyl.ServiceDefaults.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

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
        var hasServiceDefaults = context.CompilationProvider
            .Select(HasServiceDefaultsType)
            .WithTrackingName(TrackingNames.ServiceDefaultsAvailable);

        var interceptionCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(IsPotentialBuildCall, TransformToBuildInterception)
            .SelectMany(AsSingletonOrEmpty)
            .WithTrackingName(TrackingNames.InterceptionCandidates)
            .Collect()
            .WithTrackingName(TrackingNames.CollectedBuildCalls);

        context.RegisterSourceOutput(
            interceptionCandidates.Combine(hasServiceDefaults),
            EmitInterceptors);

        // Separate check for GenAI instrumentation (doesn't require full service defaults)
        var hasGenAiInstrumentation = context.CompilationProvider
            .Select(HasGenAiInstrumentationType)
            .WithTrackingName(TrackingNames.GenAiInstrumentationAvailable);

        var genAiInvocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                GenAiCallSiteAnalyzer.IsPotentialGenAiCall,
                GenAiCallSiteAnalyzer.TransformToGenAiInvocation)
            .SelectMany(AsSingletonOrEmpty)
            .WithTrackingName(TrackingNames.GenAiInvocations)
            .Collect()
            .WithTrackingName(TrackingNames.CollectedGenAiCalls);

        context.RegisterSourceOutput(
            genAiInvocations.Combine(hasGenAiInstrumentation),
            EmitGenAiInterceptors);

        var dbInvocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                DbCallSiteAnalyzer.IsPotentialDbCall,
                DbCallSiteAnalyzer.TransformToDbInvocation)
            .SelectMany(AsSingletonOrEmpty)
            .WithTrackingName(TrackingNames.DbInvocations)
            .Collect()
            .WithTrackingName(TrackingNames.CollectedDbCalls);

        context.RegisterSourceOutput(
            dbInvocations.Combine(hasServiceDefaults),
            EmitDbInterceptors);

        var otelTags = context.SyntaxProvider
            .CreateSyntaxProvider(
                OTelTagAnalyzer.IsPotentialOTelMember,
                OTelTagAnalyzer.TransformToOTelTagInfo)
            .SelectMany(AsSingletonOrEmpty)
            .WithTrackingName(TrackingNames.OTelTags)
            .Collect()
            .WithTrackingName(TrackingNames.CollectedOTelTags);

        context.RegisterSourceOutput(
            otelTags.Combine(hasServiceDefaults),
            EmitOTelTagExtensions);

        // Meter instrumentation pipeline
        var meterClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                MeterAnalyzer.IsPotentialMeterClass,
                MeterAnalyzer.TransformToMeterClassInfo)
            .SelectMany(AsSingletonOrEmpty)
            .WithTrackingName(TrackingNames.MeterClasses)
            .Collect()
            .WithTrackingName(TrackingNames.CollectedMeterClasses);

        context.RegisterSourceOutput(
            meterClasses.Combine(hasServiceDefaults),
            EmitMeterImplementations);

        // Traced instrumentation pipeline
        var tracedInvocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                TracedCallSiteAnalyzer.IsPotentialTracedCall,
                TracedCallSiteAnalyzer.TransformToTracedInvocation)
            .SelectMany(AsSingletonOrEmpty)
            .WithTrackingName(TrackingNames.TracedInvocations)
            .Collect()
            .WithTrackingName(TrackingNames.CollectedTracedCalls);

        context.RegisterSourceOutput(
            tracedInvocations.Combine(hasServiceDefaults),
            EmitTracedInterceptors);
    }

    private static bool HasServiceDefaultsType(Compilation compilation, CancellationToken _) => compilation.GetTypeByMetadataName(MetadataNames.ServiceDefaultsClass) is not null;

    private static bool HasGenAiInstrumentationType(Compilation compilation, CancellationToken _) => compilation.GetTypeByMetadataName(MetadataNames.GenAiInstrumentation) is not null;

    private static bool IsPotentialBuildCall(SyntaxNode node, CancellationToken _) => node.IsKind(SyntaxKind.InvocationExpression);

    private static ImmutableArray<T> AsSingletonOrEmpty<T>(T? item, CancellationToken _) where T : class =>
        item is not null ? [item] : [];

    private static InterceptionData? TransformToBuildInterception(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (!TryGetBuildInvocation(context, cancellationToken, out var invocation))
            return null;

        if (!IsWebApplicationBuilderBuild(invocation, context.SemanticModel.Compilation))
            return null;

        // Skip if already intercepted by another generator
        if (IsAlreadyIntercepted(context, cancellationToken))
            return null;

        var interceptLocation = GetInterceptableLocation(context, cancellationToken);
        return interceptLocation is null ? null : CreateInterceptionData(context.Node, interceptLocation);
    }

    /// <summary>
    ///     Checks if a call is already being intercepted by another source generator.
    /// </summary>
    /// <seealso href="https://github.com/dotnet/roslyn/issues/72093" />
    private static bool IsAlreadyIntercepted(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Node is not InvocationExpressionSyntax invocationSyntax)
            return false;

        var interceptor = context.SemanticModel.GetInterceptorMethod(invocationSyntax, cancellationToken);
        return interceptor is not null;
    }

    private static bool TryGetBuildInvocation(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out IInvocationOperation? invocation)
    {
        if (context.SemanticModel.GetOperation(context.Node, cancellationToken)
            is not IInvocationOperation op)
        {
            invocation = null;
            return false;
        }

        invocation = op;
        return true;
    }

    private static bool IsWebApplicationBuilderBuild(
        IInvocationOperation invocation,
        Compilation compilation)
    {
        if (invocation.TargetMethod.Name != MethodNames.Build)
            return false;

        var webAppBuilderType = compilation.GetTypeByMetadataName(MetadataNames.WebApplicationBuilder);
        return SymbolEqualityComparer.Default.Equals(
            invocation.TargetMethod.ContainingType,
            webAppBuilderType);
    }

    private static InterceptableLocation? GetInterceptableLocation(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken) =>
        context.SemanticModel.GetInterceptableLocation(
            (InvocationExpressionSyntax)context.Node,
            cancellationToken);

    private static InterceptionData CreateInterceptionData(
        SyntaxNode node,
        InterceptableLocation location) =>
        new(
            FormatLocationKey(node),
            InterceptionMethodKind.Build,
            location);

    private static string FormatLocationKey(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        var start = span.StartLinePosition;
        return $"{node.SyntaxTree.FilePath}:{start.Line}:{start.Character}";
    }

    private static void EmitInterceptors(
        SourceProductionContext context,
        (ImmutableArray<InterceptionData> Candidates, bool HasServiceDefaults) source)
    {
        if (!source.HasServiceDefaults)
            return;

        var sourceCode = BuildInterceptorsSource(source.Candidates);
        context.AddSource(OutputFileNames.Interceptors, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static void EmitGenAiInterceptors(
        SourceProductionContext context,
        (ImmutableArray<GenAiInvocationInfo> Invocations, bool HasGenAiInstrumentation) source)
    {
        if (!source.HasGenAiInstrumentation || source.Invocations.IsEmpty)
            return;

        var sourceCode = GenAiInterceptorEmitter.Emit(source.Invocations);
        if (!string.IsNullOrEmpty(sourceCode))
            context.AddSource(OutputFileNames.GenAiInterceptors, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static void EmitDbInterceptors(
        SourceProductionContext context,
        (ImmutableArray<DbInvocationInfo> Invocations, bool HasServiceDefaults) source)
    {
        if (!source.HasServiceDefaults || source.Invocations.IsEmpty)
            return;

        var sourceCode = DbInterceptorEmitter.Emit(source.Invocations);
        if (!string.IsNullOrEmpty(sourceCode))
            context.AddSource(OutputFileNames.DbInterceptors, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static void EmitOTelTagExtensions(
        SourceProductionContext context,
        (ImmutableArray<OTelTagInfo> Tags, bool HasServiceDefaults) source)
    {
        if (!source.HasServiceDefaults || source.Tags.IsEmpty)
            return;

        var sourceCode = OTelTagsEmitter.Emit(source.Tags);
        if (!string.IsNullOrEmpty(sourceCode))
            context.AddSource(OutputFileNames.OTelTagExtensions, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static void EmitMeterImplementations(
        SourceProductionContext context,
        (ImmutableArray<MeterClassInfo> Meters, bool HasServiceDefaults) source)
    {
        if (!source.HasServiceDefaults || source.Meters.IsEmpty)
            return;

        var sourceCode = MeterEmitter.Emit(source.Meters);
        if (!string.IsNullOrEmpty(sourceCode))
            context.AddSource(OutputFileNames.MeterImplementations, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static void EmitTracedInterceptors(
        SourceProductionContext context,
        (ImmutableArray<TracedInvocationInfo> Invocations, bool HasServiceDefaults) source)
    {
        if (!source.HasServiceDefaults || source.Invocations.IsEmpty)
            return;

        var sourceCode = TracedInterceptorEmitter.Emit(source.Invocations);
        if (!string.IsNullOrEmpty(sourceCode))
            context.AddSource(OutputFileNames.TracedInterceptors, SourceText.From(sourceCode, Encoding.UTF8));
    }

    private static string BuildInterceptorsSource(ImmutableArray<InterceptionData> candidates)
    {
        var sb = new StringBuilder();

        AppendFileHeader(sb);
        AppendInterceptsLocationAttribute(sb);
        AppendInterceptorsClassOpen(sb);
        AppendInterceptorMethods(sb, candidates);
        AppendInterceptorsClassClose(sb);

        return sb.ToString();
    }

    private static void AppendFileHeader(StringBuilder sb)
    {
        sb.AppendLine(SourceTemplates.AutoGeneratedHeader);
        sb.AppendLine(SourceTemplates.PragmaDisable);
        sb.AppendLine();
    }

    private static void AppendInterceptsLocationAttribute(StringBuilder sb)
    {
        sb.AppendLine(SourceTemplates.InterceptsLocationAttribute);
    }

    private static void AppendInterceptorsClassOpen(StringBuilder sb)
    {
        sb.AppendLine(SourceTemplates.InterceptorsNamespaceOpen);
    }

    private static void AppendInterceptorMethods(
        StringBuilder sb,
        ImmutableArray<InterceptionData> candidates)
    {
        var orderedCandidates = candidates
            .OrderBy(static c => c.OrderKey, StringComparer.Ordinal);

        var index = 0;
        foreach (var candidate in orderedCandidates)
        {
            if (candidate.Kind is InterceptionMethodKind.Build)
                AppendBuildInterceptor(sb, candidate, index);

            index++;
        }
    }

    private static void AppendBuildInterceptor(
        StringBuilder sb,
        InterceptionData candidate,
        int index)
    {
        var displayLocation = candidate.InterceptableLocation.GetDisplayLocation();
        var interceptAttribute = candidate.InterceptableLocation.GetInterceptsLocationAttributeSyntax();

        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static global::{{MetadataNames.WebApplication}} {{MethodNames.InterceptBuildPrefix}}{{index}}(
                                    this global::{{MetadataNames.WebApplicationBuilder}} builder)
                                {
                                    builder.{{MethodNames.TryUseConventions}}();
                                    var app = builder.{{MethodNames.Build}}();
                                    app.{{MethodNames.MapDefaultEndpoints}}();
                                    return app;
                                }
                        """);
    }

    private static void AppendInterceptorsClassClose(StringBuilder sb)
    {
        sb.AppendLine(SourceTemplates.InterceptorsNamespaceClose);
    }

    /// <summary>Fully-qualified metadata names for type lookups.</summary>
    private static class MetadataNames
    {
        public const string WebApplicationBuilder = "Microsoft.AspNetCore.Builder.WebApplicationBuilder";
        public const string WebApplication = "Microsoft.AspNetCore.Builder.WebApplication";
        public const string ServiceDefaultsClass = "Qyl.ServiceDefaults.AspNetCore.ServiceDefaults.QylServiceDefaults";
        public const string GenAiInstrumentation = "Qyl.ServiceDefaults.Instrumentation.GenAi.GenAiInstrumentation";
    }

    /// <summary>Method names used in interception and generated code.</summary>
    private static class MethodNames
    {
        public const string Build = "Build";
        public const string TryUseConventions = "TryUseQylConventions";
        public const string MapDefaultEndpoints = "MapQylDefaultEndpoints";
        public const string InterceptBuildPrefix = "Intercept_Build";
    }

    /// <summary>Tracking names for incremental generator debugging.</summary>
    private static class TrackingNames
    {
        public const string ServiceDefaultsAvailable = nameof(ServiceDefaultsAvailable);
        public const string GenAiInstrumentationAvailable = nameof(GenAiInstrumentationAvailable);
        public const string InterceptionCandidates = nameof(InterceptionCandidates);
        public const string CollectedBuildCalls = nameof(CollectedBuildCalls);
        public const string GenAiInvocations = nameof(GenAiInvocations);
        public const string CollectedGenAiCalls = nameof(CollectedGenAiCalls);
        public const string DbInvocations = nameof(DbInvocations);
        public const string CollectedDbCalls = nameof(CollectedDbCalls);
        public const string OTelTags = nameof(OTelTags);
        public const string CollectedOTelTags = nameof(CollectedOTelTags);
        public const string MeterClasses = nameof(MeterClasses);
        public const string CollectedMeterClasses = nameof(CollectedMeterClasses);
        public const string TracedInvocations = nameof(TracedInvocations);
        public const string CollectedTracedCalls = nameof(CollectedTracedCalls);
    }

    /// <summary>Output file names for generated source.</summary>
    private static class OutputFileNames
    {
        public const string Interceptors = "Intercepts.g.cs";
        public const string GenAiInterceptors = "GenAiIntercepts.g.cs";
        public const string DbInterceptors = "DbIntercepts.g.cs";
        public const string OTelTagExtensions = "OTelTagExtensions.g.cs";
        public const string MeterImplementations = "MeterImplementations.g.cs";
        public const string TracedInterceptors = "TracedIntercepts.g.cs";
    }

    /// <summary>Source code templates for generated output.</summary>
    private static class SourceTemplates
    {
        public const string AutoGeneratedHeader = "// <auto-generated/>";
        public const string PragmaDisable = "#pragma warning disable";

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
