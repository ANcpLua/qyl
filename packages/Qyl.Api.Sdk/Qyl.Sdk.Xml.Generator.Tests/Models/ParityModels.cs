using System.Xml.Serialization;
using Qyl.Xml;

namespace Qyl.Sdk.Xml.Generator.Tests.Models;

// Every type here is written twice in the parity tests: by the generated IXmlWritable and by XmlSerializer.
// XmlSerializer needs public types with public setters and List<T> or array collections.

public enum OrderStatus
{
    [XmlEnum("open")]
    Open,

    [XmlEnum("paid")]
    Paid,

    Shipped,
}

[Flags]
public enum Weekdays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    All = 7,
    Saturday = 8,
    Sunday = 16,
    Weekend = 24,
}

[GenerateXml]
[XmlRoot("order", Namespace = "urn:qyl:orders")]
public sealed partial class Order
{
    [XmlAttribute("id")]
    public int Id { get; set; }

    [XmlAttribute("status")]
    public OrderStatus Status { get; set; }

    [XmlElement("customer")]
    public Customer? Customer { get; set; }

    [XmlElement("note", Namespace = "")]
    public string? Note { get; set; }

    [XmlElement("placed")]
    public DateTime Placed { get; set; }

    [XmlElement("days")]
    public Weekdays Days { get; set; }

    [XmlArray("lines")]
    [XmlArrayItem("line")]
    public List<OrderLine>? Lines { get; set; }

    [XmlElement("tag")]
    public List<string>? Tags { get; set; }

    public int[]? Counts { get; set; }

    [XmlElement("total")]
    public Money? Total { get; set; }

    [XmlElement("ref")]
    public Guid Reference { get; set; }

    [XmlElement("weight")]
    public double? Weight { get; set; }

    [XmlElement("priority")]
    public OrderStatus? Priority { get; set; }

    [XmlElement("memo", IsNullable = true)]
    public string? Memo { get; set; }

    [XmlElement("shipping", IsNullable = true)]
    public Address? Shipping { get; set; }

    [XmlArray("scores", IsNullable = true)]
    [XmlArrayItem("score", IsNullable = false)]
    public List<int?>? Scores { get; set; }

    [XmlArray("labels")]
    [XmlArrayItem("label")]
    public List<string?>? Labels { get; set; }

    [XmlElement("alias", IsNullable = true)]
    public List<string?>? Aliases { get; set; }

    [XmlArray("stops")]
    public List<Address?>? Stops { get; set; }
}

[GenerateXml]
public sealed partial class Customer
{
    [XmlAttribute("kind")]
    public string? Kind { get; set; }

    [XmlElement("name")]
    public string? Name { get; set; }

    [XmlElement("address")]
    public Address? Address { get; set; }
}

[GenerateXml]
public sealed partial class Address
{
    [XmlElement("city")]
    public string? City { get; set; }

    [XmlElement("zip")]
    public string? Zip { get; set; }
}

[GenerateXml]
public sealed partial class OrderLine
{
    [XmlAttribute("sku")]
    public string? Sku { get; set; }

    [XmlElement("qty")]
    public int Quantity { get; set; }

    [XmlElement("price")]
    public decimal Price { get; set; }
}

[GenerateXml]
public sealed partial class Money
{
    [XmlAttribute("currency")]
    public string? Currency { get; set; }

    [XmlText]
    public decimal Amount { get; set; }
}

public class Audited
{
    [XmlAttribute("by")]
    public string? CreatedBy { get; set; }

    [XmlElement("created")]
    public DateTime Created { get; set; }
}

[GenerateXml]
[XmlRoot("document")]
public sealed partial class Document : Audited
{
    [XmlElement("title")]
    public string? Title { get; set; }
}

[GenerateXml]
[XmlRoot("node")]
public sealed partial class Node
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlElement("child")]
    public Node? Child { get; set; }
}

// XmlSerializer needs a parameterless constructor, so the sample's positional record is mirrored as a class here.
[GenerateXml]
[XmlRoot("reminder")]
public sealed partial class Reminder
{
    [XmlAttribute("id")]
    public int Id { get; set; }

    [XmlElement("title")]
    public string? Title { get; set; }

    [XmlElement("due-by")]
    public DateOnly? DueBy { get; set; }

    [XmlElement("at")]
    public TimeOnly? At { get; set; }

    [XmlElement("is-complete")]
    public bool IsComplete { get; set; }
}
