namespace Qyl.Sdk.Xml.Generator.Tests;

/// <summary>Sources the driver tests compile; the first is the sample's Todo, verbatim.</summary>
internal static class TestSources
{
    public const string Todo = """
        using System;
        using System.Xml.Serialization;
        using Qyl.Xml;

        namespace Qyl.Sample;

        [GenerateXml]
        [XmlRoot("todo")]
        public sealed partial record Todo(
            [property: XmlAttribute("id")] int Id,
            [property: XmlElement("title")] string? Title,
            [property: XmlElement("due-by")] DateOnly? DueBy = null,
            [property: XmlElement("is-complete")] bool IsComplete = false);
        """;

    public const string Tree = """
        using System;
        using System.Collections.Generic;
        using System.Xml.Serialization;
        using Qyl.Xml;

        namespace Shop;

        public enum Status { [XmlEnum("open")] Open, Paid }

        [Flags]
        public enum Days { None = 0, Mon = 1, Tue = 2 }

        public class Audited
        {
            [XmlAttribute("by")] public string? By { get; set; }
            [XmlElement("created")] public DateTime Created { get; set; }
        }

        [GenerateXml]
        [XmlRoot("order", Namespace = "urn:shop")]
        public sealed partial class Order : Audited
        {
            [XmlAttribute("id")] public int Id { get; set; }
            [XmlAttribute("status")] public Status Status { get; set; }
            [XmlElement("customer")] public Customer? Customer { get; set; }
            [XmlElement("note", Namespace = "")] public string? Note { get; set; }
            [XmlElement("days")] public Days Days { get; set; }
            [XmlArray("lines"), XmlArrayItem("line")] public List<Line>? Lines { get; set; }
            [XmlElement("tag")] public IReadOnlyList<string>? Tags { get; set; }
            public int[]? Counts { get; set; }
            [XmlElement("total")] public Money? Total { get; set; }
        }

        [GenerateXml]
        public sealed partial class Customer
        {
            [XmlElement("name")] public string? Name { get; set; }
        }

        [GenerateXml]
        public sealed partial class Line
        {
            [XmlAttribute("sku")] public string? Sku { get; set; }
            [XmlElement("qty")] public int Quantity { get; set; }
        }

        [GenerateXml]
        public sealed partial class Money
        {
            [XmlAttribute("currency")] public string? Currency { get; set; }
            [XmlText] public decimal Amount { get; set; }
        }
        """;

    public const string Unrelated = """
        namespace Elsewhere;

        public sealed class Unrelated
        {
            public int Value { get; set; }
        }
        """;

    public const string UnrelatedEdited = """
        namespace Elsewhere;

        public sealed class Unrelated
        {
            public int Value { get; set; }

            public string? Name { get; set; }
        }
        """;
}
