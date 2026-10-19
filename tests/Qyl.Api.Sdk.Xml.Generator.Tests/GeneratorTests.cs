using Microsoft.CodeAnalysis;

namespace Qyl.Api.Sdk.Xml.Generator.Tests;

[TestClass]
public sealed class GeneratorTests(TestContext testContext)
{

    [TestMethod]
    public void TodoMatchesSnapshotAndCompiles()
    {
        var run = GeneratorHarness.Run(("Todo.cs", TestSources.Todo));

        Assert.IsEmpty(run.DiagnosticIds);
        Assert.IsEmpty(run.OutputErrors);
        Snapshot.Matches(run.GeneratedSource("Qyl_Sample_Todo.GenerateXml.g.cs"), "Qyl_Sample_Todo.GenerateXml.g.cs.txt");
    }

    [TestMethod]
    public void TreeModelMatchesSnapshotAndCompiles()
    {
        var run = GeneratorHarness.Run(("Tree.cs", TestSources.Tree));

        Assert.IsEmpty(run.DiagnosticIds);
        Assert.IsEmpty(run.OutputErrors);
        Assert.AreEqual(4, run.Result.GeneratedSources.Length);
        Snapshot.Matches(run.GeneratedSource("Shop_Order.GenerateXml.g.cs"), "Shop_Order.GenerateXml.g.cs.txt");
    }

    [TestMethod]
    public void DerivedModelHidesTheBaseImplementationAndCompiles()
    {
        const string Source = """
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

        var run = GeneratorHarness.Run(("Hierarchy.cs", Source));

        Assert.IsEmpty(run.DiagnosticIds);
        Assert.IsEmpty(run.OutputErrors);
        var derived = run.GeneratedSource("Derived.GenerateXml.g.cs");
        Assert.Contains("public new static global::Qyl.Xml.XmlShape XmlShape", derived, StringComparison.Ordinal);
        Assert.Contains("public new void WriteXml(global::System.Xml.XmlWriter writer)", derived, StringComparison.Ordinal);
        Assert.Contains("WriteStartElement(\"a\")", derived, StringComparison.Ordinal);
    }

    [TestMethod]
    public void UnrelatedEditLeavesModelAndOutputCached()
    {
        var first = GeneratorHarness.CreateCompilation(("Todo.cs", TestSources.Todo), ("Unrelated.cs", TestSources.Unrelated));
        var driver = GeneratorHarness.CreateDriver().RunGenerators(first, testContext.CancellationToken);

        var unrelated = first.SyntaxTrees.Single(tree => tree.FilePath == "Unrelated.cs");
        var second = first.ReplaceSyntaxTree(unrelated, unrelated.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(TestSources.UnrelatedEdited)));
        driver = driver.RunGenerators(second, testContext.CancellationToken);

        var result = driver.GetRunResult().Results[0];
        var modelReasons = result.TrackedSteps[XmlWriterGenerator.ModelsStepName].SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();
        var outputReasons = result.TrackedOutputSteps.Values.SelectMany(steps => steps).SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();

        Assert.IsNotEmpty(modelReasons);
        Assert.IsTrue(modelReasons.All(reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged), string.Join(", ", modelReasons));
        Assert.IsTrue(outputReasons.All(reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged), string.Join(", ", outputReasons));
    }

    [TestMethod]
    public void ModelEditRegeneratesOutput()
    {
        var first = GeneratorHarness.CreateCompilation(("Todo.cs", TestSources.Todo));
        var driver = GeneratorHarness.CreateDriver().RunGenerators(first, testContext.CancellationToken);

        var todo = first.SyntaxTrees.Single();
        var edited = TestSources.Todo.Replace("[property: XmlElement(\"title\")]", "[property: XmlElement(\"name\")]", StringComparison.Ordinal);
        driver = driver.RunGenerators(
            first.ReplaceSyntaxTree(todo, todo.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(edited))),
            testContext.CancellationToken);

        var result = driver.GetRunResult().Results[0];
        var modelReasons = result.TrackedSteps[XmlWriterGenerator.ModelsStepName].SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();

        Assert.Contains(IncrementalStepRunReason.Modified, modelReasons);
        Assert.Contains("WriteStartElement(\"name\")", result.GeneratedSources.Single().SourceText.ToString(), StringComparison.Ordinal);
    }
}
