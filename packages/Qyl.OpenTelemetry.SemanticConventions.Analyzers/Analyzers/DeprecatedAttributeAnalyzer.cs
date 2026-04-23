// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

/// <summary>
/// Fires a Warning when a deprecated OTel semantic-convention ID is used as a string literal
/// in a tag-setter call, or when an <c>[Obsolete]</c> constant from
/// <c>Qyl.OpenTelemetry.SemanticConventions.*</c> is referenced. Each deprecated entry in
/// the upstream OTel registry has its own rule id (QYLSC0001..QYLSC0245) so severity can be
/// tuned per entry via <c>.editorconfig</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeprecatedAttributeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        DeprecatedDiagnostics.AllDescriptors;

    /// <inheritdoc/>
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
            // Suppress analyzer exceptions so they don't produce AD0001.
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

            // Only care about members inside Qyl.OpenTelemetry.SemanticConventions.*
            var ns = symbol.ContainingNamespace?.ToDisplayString();
            if (ns is null || !ns.StartsWith("Qyl.OpenTelemetry.SemanticConventions", StringComparison.Ordinal))
                return;

            // Check for [Obsolete] attribute.
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

            // Prefer the constant value so we can pick the precise per-entry descriptor.
            var constValue = ctx.SemanticModel.GetConstantValue(memberAccess);
            if (constValue.HasValue && constValue.Value is string strVal &&
                DeprecatedDiagnostics.ByDeprecatedId.TryGetValue(strVal, out var entry))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(entry.Descriptor, memberAccess.GetLocation(), strVal));
            }
        }
        catch
        {
            // Suppress analyzer exceptions so they don't produce AD0001.
        }
    }
}
