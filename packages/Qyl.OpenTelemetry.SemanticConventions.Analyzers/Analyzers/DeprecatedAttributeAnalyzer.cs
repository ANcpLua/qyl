
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeprecatedAttributeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        DeprecatedDiagnostics.AllDescriptors;

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);

        context.RegisterSyntaxNodeAction(
            AnalyzeMemberAccess,
            SyntaxKind.SimpleMemberAccessExpression);
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
            if (!DeprecatedDiagnostics.ByDeprecatedId.TryGetValue(value, out var entry))
                return;

            ctx.ReportDiagnostic(Diagnostic.Create(entry.Descriptor, literal.GetLocation(), value));
        }
        catch
        {
        }
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext ctx)
    {
        try
        {
            var memberAccess = (MemberAccessExpressionSyntax)ctx.Node;
            var symbol = ctx.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
            if (symbol is null)
                return;

            var ns = symbol.ContainingNamespace?.ToDisplayString();
            if (ns is null || !ns.StartsWith("Qyl.OpenTelemetry.SemanticConventions", StringComparison.Ordinal))
                return;

            var obsoleteAttributeType = ctx.Compilation.GetTypeByMetadataName("System.ObsoleteAttribute");
            if (obsoleteAttributeType is null)
                return;

            var isObsolete = false;
            foreach (var attribute in symbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, obsoleteAttributeType))
                {
                    isObsolete = true;
                    break;
                }
            }

            if (!isObsolete)
                return;

            var constValue = ctx.SemanticModel.GetConstantValue(memberAccess);
            if (constValue.HasValue && constValue.Value is string strVal &&
                DeprecatedDiagnostics.ByDeprecatedId.TryGetValue(strVal, out var entry))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(entry.Descriptor, memberAccess.GetLocation(), strVal));
            }
        }
        catch
        {
        }
    }
}
