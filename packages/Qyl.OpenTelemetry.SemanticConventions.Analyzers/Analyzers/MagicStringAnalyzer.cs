// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

/// <summary>
///     QYLSC002 — Reports an Info diagnostic when a string literal that matches a known (non-deprecated)
///     OTel attribute ID is passed to a tag-setter method. A CodeFix offers to replace it with the typed constant.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MagicStringAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic ID for magic-string usage of a known attribute.</summary>
    public const string DiagnosticId = "QYLSC002";

    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "Magic string for known OTel attribute",
        "Attribute '{0}' is a known OTel semantic convention. Use the typed constant instead of a string literal.",
        "QylSemanticConventions",
        DiagnosticSeverity.Info,
        true,
        "Typed constants from Qyl.OpenTelemetry.SemanticConventions are refactor-safe and IDE-navigable.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(s_rule);

    /// <inheritdoc />
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

            // Skip if it's deprecated — QYL-SEMCONV-001 already covers that
            if (dep.DeprecatedIds.Contains(value))
                return;

            // Only fire when the ID is in the known-valid registry
            if (!reg.IsValid(value))
                return;

            ctx.ReportDiagnostic(Diagnostic.Create(s_rule, literal.GetLocation(), value));
        }
        catch
        {
            // Suppress analyzer exceptions so they don't produce AD0001.
        }
    }
}
