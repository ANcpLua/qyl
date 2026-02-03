namespace qyl.Analyzers.CodeFixes.CodeFixes;

/// <summary>
/// Base class for all qyl code fix providers.
/// </summary>
/// <typeparam name="TNode">The syntax node type this code fix operates on.</typeparam>
public abstract partial class QylCodeFixProvider<TNode> : CodeFixProvider where TNode : CSharpSyntaxNode {
    /// <summary>Gets the FixAll provider for batch fixing.</summary>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>Registers code fixes for the given context.</summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var diagnostic = context.Diagnostics.First(d => FixableDiagnosticIds.Contains(d.Id));
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root?.FindNode(diagnostic.Location.SourceSpan) is not TNode declaration) {
            return;
        }

        var action = CreateCodeAction(context.Document, declaration, root, diagnostic);
        if (action is not null) {
            context.RegisterCodeFix(action, diagnostic);
        }
    }

    /// <summary>Creates the code action for fixing the diagnostic.</summary>
    /// <param name="document">The document containing the diagnostic.</param>
    /// <param name="syntax">The syntax node at the diagnostic location.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>A CodeAction to fix the diagnostic, or null if no fix is available.</returns>
    protected abstract CodeAction? CreateCodeAction(Document document, TNode syntax, SyntaxNode root,
        Diagnostic diagnostic);
}
