
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Qyl.Instrumentation.Generators.CallSites;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators;

[Generator]
public sealed class ServiceDefaultsSourceGenerator : IIncrementalGenerator
{
    private const string BuilderInterceptorsFile = "Intercepts.g.cs";

    private const string InterceptsLocationAttributeDeclaration = """
                                                                  using Qyl.Instrumentation;

                                                                  namespace System.Runtime.CompilerServices
                                                                  {
                                                                      [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                                                                      file sealed class InterceptsLocationAttribute(int version, string data) : global::System.Attribute
                                                                      {
                                                                          public int Version { get; } = version;
                                                                          public string Data { get; } = data;
                                                                      }
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

        var otelAutoInstrumentationAvailable = context.CompilationProvider
            .Select(GeneratorPipelineHelpers.IsOtelAutoInstrumentationReferenced)
            .WithTrackingName(PipelineStage.OtelAutoInstrumentationCheck);

        var builderCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(CouldBeBuildInvocation, ExtractBuilderCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.BuilderCallSitesDiscovered);

        var builderInput = builderCallSites.CollectAsEquatableArray()
            .Combine(runtimeAvailable)
            .Combine(otelAutoInstrumentationAvailable)
            .Select(static (input, _) =>
                new BuilderInterceptorInput(input.Left.Left, input.Left.Right, input.Right));

        context.RegisterSourceOutput(builderInput, EmitBuilderInterceptors);
    }


    private static bool CouldBeBuildInvocation(SyntaxNode node, CancellationToken _) =>
        string.Equals(IncrementalPipelineHelpers.GetInvokedMethodName(node), "Build", StringComparison.Ordinal);

    private static BuilderCallSite? ExtractBuilderCallSite(
        GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (!IncrementalPipelineHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        if (!IsWebApplicationBuilderBuildCall(invocation, context.SemanticModel.Compilation))
            return null;

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


    private static void EmitBuilderInterceptors(SourceProductionContext context, BuilderInterceptorInput input)
    {
        if (!input.QylRuntimeAvailable)
            return;

        var source = GenerateBuilderInterceptorSource(
            input.CallSites.AsImmutableArray(),
            input.OtelAutoInstrumentationAvailable);
        context.AddSource(BuilderInterceptorsFile, SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateBuilderInterceptorSource(
        ImmutableArray<BuilderCallSite> callSites,
        bool otelAutoInstrumentationAvailable)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine(GeneratedCodeHelpers.NullableEnable);
        sb.AppendLine();
        sb.AppendLine(InterceptsLocationAttributeDeclaration);
        sb.AppendLine(InterceptorsNamespaceOpen);

        var index = 0;
        foreach (var callSite in callSites.OrderBy(static c => c.SortKey, StringComparer.Ordinal))
        {
            if (callSite.Kind is BuilderCallKind.Build)
                AppendBuildInterceptorMethod(sb, callSite, index, otelAutoInstrumentationAvailable);
            index++;
        }

        sb.AppendLine(InterceptorsNamespaceClose);
        return sb.ToString();
    }

    private static void AppendBuildInterceptorMethod(
        StringBuilder sb,
        BuilderCallSite callSite,
        int index,
        bool otelAutoInstrumentationAvailable)
    {
        var displayLocation = callSite.Location.GetDisplayLocation();
        var interceptAttribute = callSite.Location.GetInterceptsLocationAttributeSyntax();

        // When Qyl.OpenTelemetry.AutoInstrumentation is referenced, route the build through its
        // QylInterceptedAspNetCore.Build wrapper (build + OTel middleware) so this stays the single
        // interceptor on the call site. The consumer opts the OTel generator out of Build() so the
        // two do not collide (CS9153). Without the package, build the host directly.
        var buildExpression = otelAutoInstrumentationAvailable
            ? $"global::{GeneratorPipelineHelpers.QylInterceptedAspNetCoreTypeName}.Build(builder)"
            : "builder.Build()";

        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static global::{{GeneratorPipelineHelpers.WebApplicationTypeName}} Intercept_Build{{index}}(
                                    this global::{{GeneratorPipelineHelpers.WebApplicationBuilderTypeName}} builder)
                                {
                                    builder.TryUseQylConventions();
                                    global::Qyl.Instrumentation.Generators.QylGeneratedRegistry.RegisterQylHostedServices(builder.Services);
                                    global::Qyl.Instrumentation.Generators.QylGeneratedRegistry.RegisterQylServices(builder.Services);
                                    global::Qyl.Instrumentation.Generators.QylGeneratedRegistry.RegisterQylHealthChecks(builder.Services);
                                    var app = {{buildExpression}};
                                    app.MapQylDefaultEndpoints();
                                    return app;
                                }
                        """);
    }

    private static class PipelineStage
    {
        public const string QylRuntimeCheck = nameof(QylRuntimeCheck);
        public const string OtelAutoInstrumentationCheck = nameof(OtelAutoInstrumentationCheck);
        public const string BuilderCallSitesDiscovered = nameof(BuilderCallSitesDiscovered);
    }

    private readonly record struct BuilderInterceptorInput(
        EquatableArray<BuilderCallSite> CallSites,
        bool QylRuntimeAvailable,
        bool OtelAutoInstrumentationAvailable);
}
