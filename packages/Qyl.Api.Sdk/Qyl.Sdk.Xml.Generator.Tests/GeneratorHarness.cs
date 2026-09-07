using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qyl.Xml;

namespace Qyl.Sdk.Xml.Generator.Tests;

/// <summary>Runs the generator over in-memory sources against the real runtime and the linked <c>Qyl.Xml</c> contract.</summary>
internal static class GeneratorHarness
{
    private static readonly ImmutableArray<MetadataReference> References = LoadReferences();

    public static CSharpCompilation CreateCompilation(params (string Path, string Source)[] files)
    {
        var trees = files.Select(file => CSharpSyntaxTree.ParseText(file.Source, new CSharpParseOptions(LanguageVersion.Latest), file.Path));

        return CSharpCompilation.Create(
            "Qyl.Generated.Tests",
            trees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    public static GeneratorDriver CreateDriver()
    {
        return CSharpGeneratorDriver.Create(
            generators: [new XmlWriterGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    public static GeneratorRun Run(params (string Path, string Source)[] files)
    {
        var compilation = CreateCompilation(files);
        var driver = CreateDriver().RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        return new GeneratorRun(driver, compilation, output, diagnostics);
    }

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var references = trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(static path =>
            {
                var name = Path.GetFileName(path);
                return name.StartsWith("System.", StringComparison.Ordinal) || name is "netstandard.dll" or "mscorlib.dll";
            })
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        // The contract types compiled into this assembly: Qyl.Xml.GenerateXmlAttribute, IXmlWritable, XmlWritableExtensions.
        references.Add(MetadataReference.CreateFromFile(typeof(IXmlWritable).Assembly.Location));

        return [.. references];
    }
}

internal sealed record GeneratorRun(GeneratorDriver Driver, Compilation Input, Compilation Output, ImmutableArray<Diagnostic> Diagnostics)
{
    public GeneratorRunResult Result => Driver.GetRunResult().Results[0];

    public string[] DiagnosticIds => [.. Diagnostics.Select(static diagnostic => diagnostic.Id).Distinct().Order(StringComparer.Ordinal)];

    public string[] OutputErrors =>
    [
        .. Output.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString()),
    ];

    public string GeneratedSource(string hintName) =>
        Result.GeneratedSources.Single(source => source.HintName == hintName).SourceText.ToString();
}
