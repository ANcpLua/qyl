
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MagicStringAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "QYLSC002";

    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "Magic string for known OTel attribute",
        "Attribute '{0}' is a known OTel semantic convention. Use the typed constant instead of a string literal.",
        "QylSemanticConventions",
        DiagnosticSeverity.Info,
        true,
        "Typed constants from Qyl.OpenTelemetry.SemanticConventions are refactor-safe and IDE-navigable.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(s_rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
    {
        try
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;
            var literal = TagMethodMatcher.TryGetStringKeyArgument(invocation);
            if (literal is null)
                return;

            var value = literal.Token.ValueText;
            var dep = DeprecationIndex.Instance;
            var reg = RegistryIndex.Instance;

            if (dep.DeprecatedIds.Contains(value))
                return;

            if (!reg.IsValid(value))
                return;

            ctx.ReportDiagnostic(Diagnostic.Create(s_rule, literal.GetLocation(), value));
        }
        catch
        {
        }
    }
}
