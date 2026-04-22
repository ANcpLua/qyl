using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

internal static class RoslynTestHelper
{
    /// <summary>
    /// Builds a minimal C# compilation from <paramref name="code"/> and returns all diagnostics
    /// emitted by <paramref name="analyzers"/>. Compilation errors are ignored — we only care
    /// about analyzer diagnostics.
    /// </summary>
    public static async Task<IReadOnlyList<Diagnostic>> GetDiagnosticsAsync(
        string code,
        params DiagnosticAnalyzer[] analyzers)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var refs = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            [tree],
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create(analyzers),
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty));

        var all = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        return all;
    }
}
