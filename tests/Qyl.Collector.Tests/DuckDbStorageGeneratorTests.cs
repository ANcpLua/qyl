using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qyl.Collector.Storage.Generators;

namespace Qyl.Collector.Tests;

public sealed class DuckDbStorageGeneratorTests
{
    [Fact]
    public void Schema_appender_and_arrow_output_share_one_deterministic_table_model()
    {
        const string source = """
                              namespace Qyl.Collector.Storage;

                              [DuckDbTable(
                                  "probe_rows",
                                  AppenderEligible = true,
                                  ArrowEligible = true,
                                  Derived = true)]
                              internal sealed partial record ProbeRow
                              {
                                  [DuckDbColumn(PrimaryKeyOrdinal = 0)]
                                  public required string Id { get; init; }

                                  public required byte[] Payload { get; init; }

                                  public string? OptionalText { get; init; }
                              }
                              """;

        var first = Generate(source);
        var second = Generate(source);

        Assert.Equal(first, second);
        Assert.Contains("\"payload\" BLOB NOT NULL", first, StringComparison.Ordinal);
        Assert.Contains("public const string DerivedHash = \"", first, StringComparison.Ordinal);
        Assert.Contains("appender.AppendRow(state, static (row, value) =>", first, StringComparison.Ordinal);
        Assert.Contains("row.AppendValue(value.Payload);", first, StringComparison.Ordinal);
        Assert.Contains("ReadArrowRowsAsync<TState>", first, StringComparison.Ordinal);
        Assert.Contains("command.UseStreamingMode = true;", first, StringComparison.Ordinal);
        Assert.Contains("ExecuteArrowBatchesAsync(cancellationToken)", first, StringComparison.Ordinal);
        Assert.Contains("((BinaryArray)batch.Column(1)).GetBytes(rowIndex).ToArray()", first,
            StringComparison.Ordinal);
        Assert.Contains("((StringArray)batch.Column(2)).GetString(rowIndex)", first,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Named_parameter_set_tracks_property_rename_from_canonical_metadata()
    {
        const string source = """
                              namespace Qyl.Collector.Storage;

                              [DuckDbTable(
                                  "probe_rows",
                                  ParameterSets = "Advance:RenamedSequence,Id")]
                              internal sealed partial record ProbeRow
                              {
                                  public ulong RenamedSequence { get; init; }
                                  public required string Id { get; init; }
                              }
                              """;

        var generated = Generate(source);

        Assert.Contains("public const string AdvanceColumnList", generated, StringComparison.Ordinal);
        Assert.Contains(
            "public const string RenamedSequenceColumnName = \"\\\"renamed_sequence\\\"\";",
            generated,
            StringComparison.Ordinal);
        Assert.Contains("\"renamed_sequence\", \"id\"", generated, StringComparison.Ordinal);
        Assert.Contains("public static void AddAdvanceParameters(", generated, StringComparison.Ordinal);
        Assert.Contains("ulong renamedSequence,", generated, StringComparison.Ordinal);
        Assert.Contains("string id)", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("\"sequence\", \"id\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Named_parameter_set_order_is_independent_of_physical_declaration_order()
    {
        const string first = """
                             namespace Qyl.Collector.Storage;
                             [DuckDbTable("probe_rows", ParameterSets = "Advance:Id,Sequence")]
                             internal sealed partial record ProbeRow
                             {
                                 public ulong Sequence { get; init; }
                                 public required string Id { get; init; }
                             }
                             """;
        const string reordered = """
                                 namespace Qyl.Collector.Storage;
                                 [DuckDbTable("probe_rows", ParameterSets = "Advance:Id,Sequence")]
                                 internal sealed partial record ProbeRow
                                 {
                                     public required string Id { get; init; }
                                     public ulong Sequence { get; init; }
                                 }
                                 """;

        foreach (var generated in new[] { Generate(first), Generate(reordered) })
        {
            Assert.Contains("\"id\", \"sequence\"", generated, StringComparison.Ordinal);
            Assert.True(
                generated.IndexOf("string id,", StringComparison.Ordinal) <
                generated.IndexOf("ulong sequence)", StringComparison.Ordinal));
        }
    }

    private static string Generate(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "GeneratorProbe",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new DuckDbInsertGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();
        Assert.Empty(result.Diagnostics.Where(static diagnostic =>
            diagnostic.Severity is DiagnosticSeverity.Error));
        return string.Join(
            "\n",
            result.Results
                .SelectMany(static generator => generator.GeneratedSources)
                .OrderBy(static generated => generated.HintName, StringComparer.Ordinal)
                .Select(static generated => generated.SourceText.ToString()));
    }
}
