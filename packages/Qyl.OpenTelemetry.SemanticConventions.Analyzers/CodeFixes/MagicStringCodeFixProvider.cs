// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.CodeFixes;

/// <summary>
///     For a magic-string that is a known attribute ID, offers to replace it with a
///     TODO comment pointing to the appropriate constant.
///     A full constant-name lookup requires the generated SemanticConventions package to be
///     present; this provider records the intent as a structured TODO so a follow-up pass
///     can resolve the exact type name.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MagicStringCodeFixProvider))]
[Shared]
public sealed class MagicStringCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(MagicStringAnalyzer.DiagnosticId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var span = diagnostic.Location.SourceSpan;
        var node = root.FindNode(span);
        if (node is not LiteralExpressionSyntax literal)
            return;

        var attrId = literal.Token.ValueText;

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Use typed constant for '{attrId}' (Qyl.OpenTelemetry.SemanticConventions)",
                ct => AnnotateWithConstantHintAsync(context.Document, literal, attrId, ct),
                $"MagicStringToConst:{attrId}"),
            diagnostic);
    }

    private static async Task<Document> AnnotateWithConstantHintAsync(
        Document document,
        LiteralExpressionSyntax literal,
        string attrId,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        // Try to resolve the constant from the SemanticModel (requires the generated constants package to be referenced)
        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is not null)
        {
            var constantExpr = TryResolveConstantExpression(semanticModel, attrId);
            if (constantExpr is not null)
            {
                var newRoot = root.ReplaceNode(literal, constantExpr.WithTriviaFrom(literal));
                return document.WithSyntaxRoot(newRoot);
            }
        }

        // Fallback: leave a structured TODO comment above the statement
        var statement = literal.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
            return document;

        var todo = SyntaxFactory.Comment(
            $"// TODO QYL-SEMCONV-002: replace \"{attrId}\" with the typed constant from Qyl.OpenTelemetry.SemanticConventions");
        var newStatement = statement.WithLeadingTrivia(
            statement.GetLeadingTrivia().Add(todo)
                .Add(SyntaxFactory.ElasticCarriageReturnLineFeed));
        var updatedRoot = root.ReplaceNode(statement, newStatement);
        return document.WithSyntaxRoot(updatedRoot);
    }

    /// <summary>Walks the compilation's global namespace to find a string const equal to <paramref name="attrId" />.</summary>
    private static ExpressionSyntax? TryResolveConstantExpression(
        SemanticModel model,
        string attrId)
    {
        // Walk types in Qyl.OpenTelemetry.SemanticConventions.* looking for a static string const with the right value
        var ns = FindNamespace(model.Compilation.GlobalNamespace, "Qyl.OpenTelemetry.SemanticConventions");
        if (ns is null)
            return null;

        foreach (var type in ns.GetTypeMembers())
        {
            var expr = TryFindConstInType(type, attrId);
            if (expr is not null)
                return expr;
        }

        return null;
    }

    private static INamespaceSymbol? FindNamespace(INamespaceSymbol root, string fullName)
    {
        var parts = fullName.Split('.');
        var current = root;
        foreach (var part in parts)
        {
            current = current.GetNamespaceMembers().FirstOrDefault(n => n.Name == part);
            if (current is null)
                return null;
        }

        return current;
    }

    private static ExpressionSyntax? TryFindConstInType(
        INamedTypeSymbol type,
        string attrId)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol { IsConst: true, Type.SpecialType: SpecialType.System_String } field)
                continue;

            if (field.ConstantValue is string val && string.Equals(val, attrId, StringComparison.Ordinal))
            {
                var access = $"{type.Name}.{field.Name}";
                return SyntaxFactory.ParseExpression(access);
            }
        }

        // Recurse into nested types
        foreach (var nested in type.GetTypeMembers())
        {
            var expr = TryFindConstInType(nested, attrId);
            if (expr is not null)
                return expr;
        }

        return null;
    }
}
