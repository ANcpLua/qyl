
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DeprecatedAttributeCodeFixProvider))]
[Shared]
public sealed class DeprecatedAttributeCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        DeprecatedDiagnostics.AllRuleIds;

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var span = diagnostic.Location.SourceSpan;
        var literal = root.FindToken(span.Start).Parent?
            .AncestorsAndSelf()
            .OfType<LiteralExpressionSyntax>()
            .FirstOrDefault();
        if (literal is null)
            return;

        var deprecatedId = literal.Token.ValueText;
        if (!DeprecatedDiagnostics.ByDeprecatedId.TryGetValue(deprecatedId, out var entry))
            return;

        switch (entry.Mode)
        {
            case DeprecatedReplacementMode.Direct
                or DeprecatedReplacementMode.FieldMapping
                or DeprecatedReplacementMode.Integrate:
                if (entry.Replacements.Length > 0)
                    RegisterDirect(context, literal, entry, diagnostic);
                break;

            case DeprecatedReplacementMode.Alternative:
                foreach (var choice in entry.Replacements)
                    RegisterDirect(context, literal, entry, diagnostic, choice);
                break;

            case DeprecatedReplacementMode.Removed:
                RegisterRemoval(context, literal, entry, diagnostic);
                break;

            case DeprecatedReplacementMode.Composite:
            case DeprecatedReplacementMode.Conditional:
            case DeprecatedReplacementMode.Contextual:
            case DeprecatedReplacementMode.Example:
            case DeprecatedReplacementMode.NoteOnly:
                break;
        }
    }

    private static void RegisterDirect(
        CodeFixContext context,
        LiteralExpressionSyntax literal,
        DeprecatedEntry entry,
        Diagnostic diagnostic,
        string? explicitReplacement = null)
    {
        var replacement = explicitReplacement ?? entry.Replacements[0];
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Replace '{entry.DeprecatedId}' with '{replacement}'",
                ct => ReplaceLiteralAsync(context.Document, literal, replacement, ct),
                $"{entry.RuleId}:replace:{replacement}"),
            diagnostic);
    }

    private static void RegisterRemoval(
        CodeFixContext context,
        LiteralExpressionSyntax literal,
        DeprecatedEntry entry,
        Diagnostic diagnostic)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Remove deprecated '{entry.DeprecatedId}' usage",
                ct => RemoveEnclosingStatementAsync(context.Document, literal, entry.DeprecatedId, ct),
                $"{entry.RuleId}:remove"),
            diagnostic);
    }

    private static async Task<Document> ReplaceLiteralAsync(
        Document document,
        LiteralExpressionSyntax literal,
        string replacement,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var newLiteral = SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(replacement))
            .WithTriviaFrom(literal);

        var newRoot = root.ReplaceNode(literal, newLiteral);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> RemoveEnclosingStatementAsync(
        Document document,
        LiteralExpressionSyntax literal,
        string deprecatedId,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var statement = literal.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
            return document;

        var leading = statement.GetLeadingTrivia();
        var comment = SyntaxFactory.Comment(
            $"// TODO: '{deprecatedId}' was removed from the OTel registry with no replacement.");

        var newTrivia = leading
            .Add(comment)
            .Add(SyntaxFactory.CarriageReturnLineFeed);

        var replacementStatement = SyntaxFactory.EmptyStatement()
            .WithLeadingTrivia(newTrivia)
            .WithTrailingTrivia(statement.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(statement, replacementStatement);
        return document.WithSyntaxRoot(newRoot);
    }
}
