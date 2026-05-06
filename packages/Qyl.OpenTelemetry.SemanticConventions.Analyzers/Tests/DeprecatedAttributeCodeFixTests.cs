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
    public async Task DirectModeReplacesLiteralAsync()
    {
        const string code = """
                            class C { void M(object a) { a.SetTag("android.state", "x"); } }
                            """;
        var (after, title) = await ApplyFirstFixAsync(code).ConfigureAwait(true);

        Assert.Contains("\"android.app.state\"", after, StringComparison.Ordinal);
        Assert.DoesNotContain("\"android.state\"", after, StringComparison.Ordinal);
        Assert.StartsWith("Replace 'android.state' with 'android.app.state'", title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AlternativeModeOffersOneActionPerReplacementAsync()
    {
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
    public async Task RemovedModeStripsStatementAsync()
    {
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
    public async Task CompositeModeOffersNoFixAsync()
    {
        const string code = """
                            class C { void M(object a) { a.SetTag("db.connection_string", "x"); } }
                            """;
        var actions = await GetFixActionsAsync(code).ConfigureAwait(true);

        Assert.Empty(actions);
    }

    [Fact]
    public void AllAnalyzerRuleIdsAreFixable()
    {
        Assert.Equal(s_analyzer.SupportedDiagnostics.Length, s_fix.FixableDiagnosticIds.Length);
        foreach (var descriptor in s_analyzer.SupportedDiagnostics)
            Assert.Contains(descriptor.Id, s_fix.FixableDiagnosticIds);
    }

    private static async Task<ImmutableArray<CodeAction>> GetFixActionsAsync(string code)
    {
        using var fixture = await PrepareDocumentAsync(code).ConfigureAwait(true);
        Assert.Single(fixture.Diagnostics);

        var actions = ImmutableArray.CreateBuilder<CodeAction>();
        var context = new CodeFixContext(
            fixture.Document,
            fixture.Diagnostics[0],
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await s_fix.RegisterCodeFixesAsync(context).ConfigureAwait(true);
        return actions.ToImmutable();
    }

    private static async Task<(string Text, string Title)> ApplyFirstFixAsync(string code)
    {
        using var fixture = await PrepareDocumentAsync(code).ConfigureAwait(true);
        Assert.Single(fixture.Diagnostics);

        CodeAction? captured = null;
        var context = new CodeFixContext(
            fixture.Document,
            fixture.Diagnostics[0],
            (action, _) => { captured ??= action; },
            CancellationToken.None);

        await s_fix.RegisterCodeFixesAsync(context).ConfigureAwait(true);
        Assert.NotNull(captured);

        var operations = await captured!.GetOperationsAsync(CancellationToken.None).ConfigureAwait(true);
        var applyOp = operations.OfType<ApplyChangesOperation>().First();
        var changedDoc = applyOp.ChangedSolution.GetDocument(fixture.Document.Id)!;
        var text = await changedDoc.GetTextAsync().ConfigureAwait(true);
        return (text.ToString(), captured.Title);
    }

    private static async Task<WorkspaceFixture> PrepareDocumentAsync(string code)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        workspace.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Default,
            "Test",
            "Test",
            LanguageNames.CSharp,
            metadataReferences: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location)
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
        return new WorkspaceFixture(workspace) { Document = document, Diagnostics = all };
    }

    private sealed class WorkspaceFixture(AdhocWorkspace workspace) : IDisposable
    {
        internal required Document Document { get; init; }
        internal required ImmutableArray<Diagnostic> Diagnostics { get; init; }
        public void Dispose() => workspace.Dispose();
    }
}
