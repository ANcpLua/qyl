using Qyl.Instrumentation.Generators.Loom.Extraction;
using Qyl.Instrumentation.Generators.Loom.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Qyl.Instrumentation.Generators.Loom;

[Generator]
public sealed class LoomSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tools = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Qyl.Instrumentation.Instrumentation.Loom.LoomToolAttribute",
                static (node, _) => node is MethodDeclarationSyntax,
                LoomToolExtractor.Extract)
            .WhereNotNull()
            .CollectAsEquatableArray();

        var contracts = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Qyl.Instrumentation.Instrumentation.Loom.LoomContractAttribute",
                static (node, _) => node is TypeDeclarationSyntax,
                LoomContractExtractor.Extract)
            .WhereNotNull()
            .CollectAsEquatableArray();

        var steps = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Qyl.Instrumentation.Instrumentation.Loom.LoomStepAttribute",
                static (node, _) => node is TypeDeclarationSyntax,
                LoomStepExtractor.Extract)
            .WhereNotNull()
            .CollectAsEquatableArray();

        var workflows = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Qyl.Instrumentation.Instrumentation.Loom.LoomWorkflowAttribute",
                static (node, _) => node is TypeDeclarationSyntax,
                LoomWorkflowExtractor.Extract)
            .WhereNotNull()
            .CollectAsEquatableArray();

        RegisterPartialValidation(context,
            "Qyl.Instrumentation.Instrumentation.Loom.LoomToolAttribute",
            static (node, _) => node is MethodDeclarationSyntax);

        RegisterPartialValidation(context,
            "Qyl.Instrumentation.Instrumentation.Loom.LoomContractAttribute",
            static (node, _) => node is TypeDeclarationSyntax);

        RegisterPartialValidation(context,
            "Qyl.Instrumentation.Instrumentation.Loom.LoomStepAttribute",
            static (node, _) => node is TypeDeclarationSyntax);

        RegisterPartialValidation(context,
            "Qyl.Instrumentation.Instrumentation.Loom.LoomWorkflowAttribute",
            static (node, _) => node is TypeDeclarationSyntax);

        var hasLoomTypes = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName(
                "Qyl.Instrumentation.Instrumentation.Loom.LoomToolDescriptor") is not null);

        context.RegisterSourceOutput(
            tools.Combine(contracts).Combine(steps).Combine(workflows).Combine(hasLoomTypes),
            static (spc, input) =>
            {
                var ((((tools, contracts), steps), workflows), hasLoomTypes) = input;

                if (!hasLoomTypes)
                    return;

                foreach (var group in tools.GroupBy(static tool => tool.ContainingTypeFullyQualified,
                             StringComparer.Ordinal))
                {
                    var first = group.First();
                    var source = LoomToolOutputGenerator.Generate(
                        group.Key,
                        first.DeclarationChain,
                        group.ToArray().ToEquatableArray());

                    spc.AddSource(
                        LoomGenerationHelpers.HintName(group.Key, ".LoomTools.g.cs"),
                        SourceText.From(source, Encoding.UTF8));
                }

                foreach (var contract in contracts)
                {
                    var source = LoomContractOutputGenerator.Generate(contract);
                    spc.AddSource(
                        LoomGenerationHelpers.HintName(contract.FullyQualifiedTypeName, ".LoomContract.g.cs"),
                        SourceText.From(source, Encoding.UTF8));
                }

                foreach (var step in steps)
                {
                    var source = LoomStepOutputGenerator.Generate(step);
                    spc.AddSource(
                        LoomGenerationHelpers.HintName(step.ExecutorTypeFullyQualified, ".LoomStep.g.cs"),
                        SourceText.From(source, Encoding.UTF8));
                }

                foreach (var workflow in workflows)
                {
                    var source = LoomWorkflowOutputGenerator.Generate(workflow);
                    spc.AddSource(
                        LoomGenerationHelpers.HintName(workflow.WorkflowTypeFullyQualified, ".LoomWorkflow.g.cs"),
                        SourceText.From(source, Encoding.UTF8));
                }

                var registry = LoomRegistryOutputGenerator.Generate(tools, contracts, steps, workflows);
                spc.AddSource(
                    "Qyl.Instrumentation.Instrumentation.Loom.LoomGeneratedRegistry.g.cs",
                    SourceText.From(registry, Encoding.UTF8));

                var telemetryManifest =
                    LoomTelemetryManifestOutputGenerator.Generate(tools, contracts, steps, workflows);
                spc.AddSource(
                    "Qyl.Instrumentation.Instrumentation.Loom.LoomGeneratedRegistry.TelemetryManifest.g.cs",
                    SourceText.From(telemetryManifest, Encoding.UTF8));
            });
    }

    private static void RegisterPartialValidation(
        IncrementalGeneratorInitializationContext context,
        string fullyQualifiedMetadataName,
        Func<SyntaxNode, CancellationToken, bool> predicate)
    {
        var validationFlows = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName,
                predicate,
                static (syntaxContext, cancellationToken) =>
                {
                    var declaration = syntaxContext.TargetNode as TypeDeclarationSyntax
                                      ?? syntaxContext.TargetNode.Parent as TypeDeclarationSyntax;

                    return declaration is not null
                        ? LoomDeclarationChainExtractor.ExtractWithDiagnostics(declaration, cancellationToken)
                        : default;
                });

        context.RegisterSourceOutput(validationFlows, static (spc, flow) => flow.Report(spc));
    }
}
