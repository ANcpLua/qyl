using Microsoft.CodeAnalysis;

namespace Qyl.Sdk.Xml.Generator.Tests;

public sealed class GeneratorTests
{
    [Fact]
    public void TodoMatchesSnapshotAndCompiles()
    {
        var run = GeneratorHarness.Run(("Todo.cs", TestSources.Todo));

        Assert.Empty(run.DiagnosticIds);
        Assert.Empty(run.OutputErrors);
        Snapshot.Matches(run.GeneratedSource("Qyl_Sample_Todo.GenerateXml.g.cs"), "Qyl_Sample_Todo.GenerateXml.g.cs.txt");
    }

    [Fact]
    public void TreeModelMatchesSnapshotAndCompiles()
    {
        var run = GeneratorHarness.Run(("Tree.cs", TestSources.Tree));

        Assert.Empty(run.DiagnosticIds);
        Assert.Empty(run.OutputErrors);
        Assert.Equal(4, run.Result.GeneratedSources.Length);
        Snapshot.Matches(run.GeneratedSource("Shop_Order.GenerateXml.g.cs"), "Shop_Order.GenerateXml.g.cs.txt");
    }

    [Fact]
    public void DerivedModelHidesTheBaseImplementationAndCompiles()
    {
        const string source = """
            using System.Xml.Serialization;
            using Qyl.Xml;

            [GenerateXml]
            [XmlRoot("base")]
            public partial class Base
            {
                [XmlElement("a")] public int A { get; set; }
            }

            [GenerateXml]
            [XmlRoot("derived")]
            public partial class Derived : Base
            {
                [XmlElement("b")] public int B { get; set; }
            }
            """;

        var run = GeneratorHarness.Run(("Hierarchy.cs", source));

        Assert.Empty(run.DiagnosticIds);
        Assert.Empty(run.OutputErrors);
        var derived = run.GeneratedSource("Derived.GenerateXml.g.cs");
        Assert.Contains("public new static global::Qyl.Xml.XmlShape XmlShape", derived, StringComparison.Ordinal);
        Assert.Contains("public new void WriteXml(global::System.Xml.XmlWriter writer)", derived, StringComparison.Ordinal);
        Assert.Contains("WriteStartElement(\"a\")", derived, StringComparison.Ordinal);
    }

    [Fact]
    public void UnrelatedEditLeavesModelAndOutputCached()
    {
        var first = GeneratorHarness.CreateCompilation(("Todo.cs", TestSources.Todo), ("Unrelated.cs", TestSources.Unrelated));
        var driver = GeneratorHarness.CreateDriver().RunGenerators(first, TestContext.Current.CancellationToken);

        var unrelated = first.SyntaxTrees.Single(tree => tree.FilePath == "Unrelated.cs");
        var second = first.ReplaceSyntaxTree(unrelated, unrelated.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(TestSources.UnrelatedEdited)));
        driver = driver.RunGenerators(second, TestContext.Current.CancellationToken);

        var result = driver.GetRunResult().Results[0];
        var modelReasons = result.TrackedSteps[XmlWriterGenerator.ModelsStepName].SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();
        var outputReasons = result.TrackedOutputSteps.Values.SelectMany(steps => steps).SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();

        Assert.NotEmpty(modelReasons);
        Assert.True(modelReasons.All(reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged), string.Join(", ", modelReasons));
        Assert.True(outputReasons.All(reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged), string.Join(", ", outputReasons));
    }

    [Fact]
    public void ModelEditRegeneratesOutput()
    {
        var first = GeneratorHarness.CreateCompilation(("Todo.cs", TestSources.Todo));
        var driver = GeneratorHarness.CreateDriver().RunGenerators(first, TestContext.Current.CancellationToken);

        var todo = first.SyntaxTrees.Single();
        var edited = TestSources.Todo.Replace("[property: XmlElement(\"title\")]", "[property: XmlElement(\"name\")]", StringComparison.Ordinal);
        driver = driver.RunGenerators(
            first.ReplaceSyntaxTree(todo, todo.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(edited))),
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult().Results[0];
        var modelReasons = result.TrackedSteps[XmlWriterGenerator.ModelsStepName].SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();

        Assert.Contains(IncrementalStepRunReason.Modified, modelReasons);
        Assert.Contains("WriteStartElement(\"name\")", result.GeneratedSources.Single().SourceText.ToString(), StringComparison.Ordinal);
    }
}
