using Microsoft.CodeAnalysis;

namespace Qyl.Sdk.Xml.Generator.Tests;

[TestClass]
public sealed class GeneratorTests
{
    [TestMethod]
    public void TodoMatchesSnapshotAndCompiles()
    {
        var run = GeneratorHarness.Run(("Todo.cs", TestSources.Todo));

        CollectionAssert.AreEqual(Array.Empty<string>(), run.DiagnosticIds);
        CollectionAssert.AreEqual(Array.Empty<string>(), run.OutputErrors);
        Snapshot.Matches(run.GeneratedSource("Qyl_Sample_Todo.GenerateXml.g.cs"), "Qyl_Sample_Todo.GenerateXml.g.cs.txt");
    }

    [TestMethod]
    public void TreeModelMatchesSnapshotAndCompiles()
    {
        var run = GeneratorHarness.Run(("Tree.cs", TestSources.Tree));

        CollectionAssert.AreEqual(Array.Empty<string>(), run.DiagnosticIds);
        CollectionAssert.AreEqual(Array.Empty<string>(), run.OutputErrors);
        Assert.AreEqual(4, run.Result.GeneratedSources.Length);
        Snapshot.Matches(run.GeneratedSource("Shop_Order.GenerateXml.g.cs"), "Shop_Order.GenerateXml.g.cs.txt");
    }

    [TestMethod]
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

        CollectionAssert.AreEqual(Array.Empty<string>(), run.DiagnosticIds);
        CollectionAssert.AreEqual(Array.Empty<string>(), run.OutputErrors);
        var derived = run.GeneratedSource("Derived.GenerateXml.g.cs");
        StringAssert.Contains(derived, "public new static global::Qyl.Xml.XmlShape XmlShape", StringComparison.Ordinal);
        StringAssert.Contains(derived, "public new void WriteXml(global::System.Xml.XmlWriter writer)", StringComparison.Ordinal);
        StringAssert.Contains(derived, "WriteStartElement(\"a\")", StringComparison.Ordinal);
    }

    [TestMethod]
    public void UnrelatedEditLeavesModelAndOutputCached()
    {
        var first = GeneratorHarness.CreateCompilation(("Todo.cs", TestSources.Todo), ("Unrelated.cs", TestSources.Unrelated));
        var driver = GeneratorHarness.CreateDriver().RunGenerators(first);

        var unrelated = first.SyntaxTrees.Single(tree => tree.FilePath == "Unrelated.cs");
        var second = first.ReplaceSyntaxTree(unrelated, unrelated.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(TestSources.UnrelatedEdited)));
        driver = driver.RunGenerators(second);

        var result = driver.GetRunResult().Results[0];
        var modelReasons = result.TrackedSteps[XmlWriterGenerator.ModelsStepName].SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();
        var outputReasons = result.TrackedOutputSteps.Values.SelectMany(steps => steps).SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();

        Assert.AreNotEqual(0, modelReasons.Length);
        Assert.IsTrue(modelReasons.All(reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged), string.Join(", ", modelReasons));
        Assert.IsTrue(outputReasons.All(reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged), string.Join(", ", outputReasons));
    }

    [TestMethod]
    public void ModelEditRegeneratesOutput()
    {
        var first = GeneratorHarness.CreateCompilation(("Todo.cs", TestSources.Todo));
        var driver = GeneratorHarness.CreateDriver().RunGenerators(first);

        var todo = first.SyntaxTrees.Single();
        var edited = TestSources.Todo.Replace("[property: XmlElement(\"title\")]", "[property: XmlElement(\"name\")]", StringComparison.Ordinal);
        driver = driver.RunGenerators(first.ReplaceSyntaxTree(todo, todo.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(edited))));

        var result = driver.GetRunResult().Results[0];
        var modelReasons = result.TrackedSteps[XmlWriterGenerator.ModelsStepName].SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();

        CollectionAssert.Contains(modelReasons, IncrementalStepRunReason.Modified);
        StringAssert.Contains(result.GeneratedSources.Single().SourceText.ToString(), "WriteStartElement(\"name\")", StringComparison.Ordinal);
    }
}
