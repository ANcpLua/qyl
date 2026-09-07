namespace Qyl.Sdk.Xml.Generator.Tests;

public sealed class DiagnosticTests
{
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.ComponentModel;
        using System.Xml.Serialization;
        using Qyl.Xml;

        """;

    [Theory]
    [InlineData("QYLXML001", "public partial class Outer { [GenerateXml] public partial class Model { public int Id { get; set; } } }")]
    [InlineData("QYLXML002", "[GenerateXml] public partial class Model { public int this[int index] => index; }")]
    [InlineData("QYLXML003", "[GenerateXml] public partial class Model { public object? Value { get; set; } }")]
    [InlineData("QYLXML003", "[GenerateXml] public partial class Model { public byte[]? Blob { get; set; } }")]
    [InlineData("QYLXML003", "[GenerateXml] public partial class Model { public Dictionary<string, int>? Map { get; set; } }")]
    [InlineData("QYLXML004", "[GenerateXml] public partial class Model { [XmlElement(\"a\"), XmlAttribute(\"a\")] public int Id { get; set; } }")]
    [InlineData("QYLXML004", "[GenerateXml] public partial class Model { [XmlElement(\"a\"), XmlArrayItem(\"i\")] public List<int>? Ids { get; set; } }")]
    [InlineData("QYLXML005", "[GenerateXml] public partial class Model { [XmlElement(\"1bad\")] public int Id { get; set; } }")]
    [InlineData("QYLXML006", "[GenerateXml] [XmlType(\"m\")] public partial class Model { public int Id { get; set; } }")]
    [InlineData("QYLXML006", "[GenerateXml] public partial class Model { [XmlElement(\"a\", Order = 1)] public string? Name { get; set; } }")]
    [InlineData("QYLXML006", "[GenerateXml] public partial class Model { [XmlAttribute(\"a\", DataType = \"token\")] public string? Name { get; set; } }")]
    [InlineData("QYLXML006", "[GenerateXml] [XmlRoot(\"m\", IsNullable = true)] public partial class Model { public int Id { get; set; } }")]
    [InlineData("QYLXML006", "[GenerateXml] public partial class Model { [XmlAnyElement] public string? Name { get; set; } }")]
    [InlineData("QYLXML007", "[GenerateXml] public class Model { public int Id { get; set; } }")]
    [InlineData("QYLXML008", "[GenerateXml] public partial class Model : Exception { public int Id { get; set; } }")]
    [InlineData("QYLXML009", "[GenerateXml] public partial class Model { [XmlAttribute(\"c\")] public Child? Child { get; set; } } [GenerateXml] public partial class Child { }")]
    [InlineData("QYLXML009", "[GenerateXml] public partial class Model { [XmlArray(\"a\")] public int Id { get; set; } }")]
    [InlineData("QYLXML009", "[GenerateXml] public partial class Model { [XmlText] public int A { get; set; } [XmlText] public int B { get; set; } }")]
    [InlineData("QYLXML010", "[GenerateXml] public partial class Model { public List<DateOnly>? Dates { get; set; } }")]
    [InlineData("QYLXML011", "[GenerateXml] public partial class Model { public int Id { get; set; } public bool ShouldSerializeId() => true; }")]
    [InlineData("QYLXML011", "[GenerateXml] public partial class Model { public int Id { get; set; } [XmlIgnore] public bool IdSpecified { get; set; } }")]
    [InlineData("QYLXML011", "[GenerateXml] public partial class Model { [DefaultValue(0)] public int Id { get; set; } }")]
    public void ReportsExactlyOneRule(string expectedId, string source)
    {
        var run = GeneratorHarness.Run(("Model.cs", Usings + source));

        Assert.True(
            run.DiagnosticIds is [var reported] && reported == expectedId,
            string.Join(Environment.NewLine, run.Diagnostics));
        Assert.False(run.Result.GeneratedSources.Any(static source => source.HintName == "Model.GenerateXml.g.cs"), "no source is generated for a model with errors");
    }

    [Fact]
    public void HandledOptionsProduceNoDiagnostics()
    {
        const string source = """
            [GenerateXml]
            [XmlRoot(ElementName = "m", Namespace = "urn:m")]
            public partial class Model
            {
                [XmlAttribute(AttributeName = "id", Namespace = "urn:a")] public int Id { get; set; }
                [XmlElement(ElementName = "name", Namespace = "", IsNullable = true)] public string? Name { get; set; }
                [XmlElement("when", IsNullable = false)] public DateTime? When { get; set; }
                [XmlArray(ElementName = "items", Namespace = "urn:i", IsNullable = true), XmlArrayItem(ElementName = "item", Namespace = "urn:i", IsNullable = false)] public List<int?>? Items { get; set; }
                [XmlElement("flat", IsNullable = true)] public List<string>? Flat { get; set; }
                [XmlArrayItem("kid")] public List<Kid>? Kids { get; set; }
                public List<Kid>? Defaults { get; set; }
                [XmlIgnore] public object? Ignored { get; set; }
                [XmlElement("level")] public Level Level { get; set; }
            }

            [GenerateXml]
            public partial class Kid { [XmlText] public string? Text { get; set; } }

            public enum Level { [XmlEnum(Name = "lo")] Low, [XmlEnum("hi")] High, [XmlIgnore] Hidden }
            """;

        var run = GeneratorHarness.Run(("Model.cs", Usings + source));

        Assert.True(run.DiagnosticIds.Length is 0, string.Join(Environment.NewLine, run.Diagnostics));
        Assert.Empty(run.OutputErrors);
    }
}
