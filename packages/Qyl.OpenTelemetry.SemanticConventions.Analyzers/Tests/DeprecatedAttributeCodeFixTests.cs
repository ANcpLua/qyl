using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.CodeFixes;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public sealed class DeprecatedAttributeCodeFixTests
{
    private static readonly DeprecatedAttributeAnalyzer s_analyzer = new();
    private static readonly DeprecatedAttributeCodeFixProvider s_fix = new();

    [Fact]
    public async Task Direct_mode_replaces_literalAsync()
    {
        // android.state (mode=direct, replacements=[android.app.state])
        const string code = """
            class C { void M(object a) { a.SetTag("android.state", "x"); } }
            """;
        var (after, title) = await ApplyFirstFixAsync(code).ConfigureAwait(true);

        Assert.Contains("\"android.app.state\"", after, StringComparison.Ordinal);
        Assert.DoesNotContain("\"android.state\"", after, StringComparison.Ordinal);
        Assert.StartsWith("Replace 'android.state' with 'android.app.state'", title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Alternative_mode_offers_one_action_per_replacementAsync()
    {
        // http.host (mode=alternative, 3 replacements)
        const string code = """
            class C { void M(object a) { a.SetTag("http.host", "x"); } }
            """;
        var actions = await GetFixActionsAsync(code).ConfigureAwait(true);

        var entry = DeprecatedDiagnostics.ByDeprecatedId["http.host"];
        Assert.Equal(entry.Replacements.Length, actions.Length);
        foreach (var replacement in entry.Replacements)
            Assert.Contains(actions, a => a.Title.Contains($"'{replacement}'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Field_mapping_mode_has_no_autofix_when_replacements_emptyAsync()
    {
        // The only field_mapping entry in the registry (event.name) has no replacement target
        // because the migration is structural (value moves to LogRecord.EventName).
        var fmEntry = DeprecatedDiagnostics.ByDeprecatedId.Values
            .Single(e => e.Mode == DeprecatedReplacementMode.FieldMapping);
        Assert.Empty(fmEntry.Replacements);
        var code = $$"""
            class C { void M(object a) { a.SetTag("{{fmEntry.DeprecatedId}}", "x"); } }
            """;
        var actions = await GetFixActionsAsync(code).ConfigureAwait(true);
        Assert.Empty(actions);
    }

    [Fact]
    public async Task Removed_mode_strips_statementAsync()
    {
        // db.jdbc.driver_classname (mode=removed)
        const string code = """
            class C
            {
                void M(object a)
                {
                    a.SetTag("db.jdbc.driver_classname", "x");
                }
            }
            """;
        var (after, title) = await ApplyFirstFixAsync(code).ConfigureAwait(true);

        Assert.Contains("TODO:", after, StringComparison.Ordinal);
        Assert.Contains("db.jdbc.driver_classname", after, StringComparison.Ordinal);
        Assert.DoesNotContain("a.SetTag(\"db.jdbc.driver_classname\"", after, StringComparison.Ordinal);
        Assert.StartsWith("Remove deprecated 'db.jdbc.driver_classname' usage", title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Composite_mode_offers_no_fixAsync()
    {
        // db.connection_string (mode=composite)
        const string code = """
            class C { void M(object a) { a.SetTag("db.connection_string", "x"); } }
            """;
        var actions = await GetFixActionsAsync(code).ConfigureAwait(true);

        Assert.Empty(actions);
    }

    [Fact]
    public void All_245_rule_ids_are_fixableAsync()
    {
        // Guarantees that if the diagnostic fires, the codefix at least sees it — even when
        // the mode has no auto-action. This is the contract: every deprecated ID is routable.
        Assert.Equal(245, s_fix.FixableDiagnosticIds.Length);
        foreach (var descriptor in s_analyzer.SupportedDiagnostics)
            Assert.Contains(descriptor.Id, s_fix.FixableDiagnosticIds);
    }

    private static async Task<ImmutableArray<CodeAction>> GetFixActionsAsync(string code)
    {
        var (document, diagnostics) = await PrepareDocumentAsync(code).ConfigureAwait(true);
        Assert.Single(diagnostics);

        var actions = ImmutableArray.CreateBuilder<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostics[0],
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await s_fix.RegisterCodeFixesAsync(context).ConfigureAwait(true);
        return actions.ToImmutable();
    }

    private static async Task<(string Text, string Title)> ApplyFirstFixAsync(string code)
    {
        var (document, diagnostics) = await PrepareDocumentAsync(code).ConfigureAwait(true);
        Assert.Single(diagnostics);

        CodeAction? captured = null;
        var context = new CodeFixContext(
            document,
            diagnostics[0],
            (action, _) => { captured ??= action; },
            CancellationToken.None);

        await s_fix.RegisterCodeFixesAsync(context).ConfigureAwait(true);
        Assert.NotNull(captured);

        var operations = await captured!.GetOperationsAsync(CancellationToken.None).ConfigureAwait(true);
        var applyOp = operations.OfType<ApplyChangesOperation>().First();
        var changedDoc = applyOp.ChangedSolution.GetDocument(document.Id)!;
        var text = await changedDoc.GetTextAsync().ConfigureAwait(true);
        return (text.ToString(), captured.Title);
    }

    private static async Task<(Document Document, ImmutableArray<Diagnostic> Diagnostics)> PrepareDocumentAsync(string code)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var project = workspace.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Default,
            "Test",
            "Test",
            LanguageNames.CSharp,
            metadataReferences: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            },
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));

        var document = workspace.AddDocument(DocumentInfo.Create(
            documentId,
            "Test.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(code), VersionStamp.Default))));

        var compilation = await document.Project.GetCompilationAsync().ConfigureAwait(true);
        var withAnalyzers = compilation!.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(s_analyzer),
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty));

        var all = await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(true);
        return (document, all);
    }
}
