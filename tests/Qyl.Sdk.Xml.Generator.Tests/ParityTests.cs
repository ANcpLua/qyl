using System.Xml;
using System.Xml.Serialization;
using Qyl.Sdk.Xml.Generator.Tests.Models;
using Qyl.Xml;

namespace Qyl.Sdk.Xml.Generator.Tests;

/// <summary>XmlSerializer is the oracle: the generated writer must produce the same document for the same attributes.</summary>
public sealed class ParityTests
{
    private static readonly XmlWriterSettings Settings = new()
    {
        OmitXmlDeclaration = true,
        ConformanceLevel = ConformanceLevel.Document,
        Indent = false,
    };

    private static readonly DateTime Placed = new(2026, 9, 7, 10, 30, 0, DateTimeKind.Utc);

    private static readonly string[] StatusNames = ["open", "paid", "Shipped"];

    [Fact]
    public void TreeWithNamespacesEnumsCollectionsAndText()
    {
        var order = new Order
        {
            Id = 7,
            Status = OrderStatus.Paid,
            Customer = new Customer { Kind = "vip", Name = "Ada", Address = new Address { City = "Wien", Zip = "1010" } },
            Note = "leave at door",
            Placed = Placed,
            Days = Weekdays.Monday | Weekdays.Tuesday,
            Lines = [new OrderLine { Sku = "A-1", Quantity = 2, Price = 9.99m }, new OrderLine { Sku = "B-2", Quantity = 1, Price = 0.5m }],
            Tags = ["fragile", "gift"],
            Counts = [1, 2, 3],
            Total = new Money { Currency = "EUR", Amount = 20.48m },
            Reference = new Guid("8d3f2c6a-1b2e-4c5d-9e8f-0a1b2c3d4e5f"),
            Weight = 1.25,
            Priority = OrderStatus.Open,
        };

        AssertParity(order);
    }

    [Fact]
    public void AbsentMembersAndEmptyCollections()
    {
        var order = new Order
        {
            Id = 1,
            Status = OrderStatus.Shipped,
            Placed = Placed,
            Days = Weekdays.None,
            Lines = [],
            Tags = [],
            Counts = [],
        };

        AssertParity(order);
    }

    [Theory]
    [InlineData(Weekdays.All)]
    [InlineData(Weekdays.Weekend)]
    [InlineData(Weekdays.Monday | Weekdays.Saturday | Weekdays.Sunday)]
    [InlineData(Weekdays.Monday | Weekdays.Wednesday)]
    public void FlagsFollowXmlSerializerMemberSelection(Weekdays days)
    {
        AssertParity(new Order { Id = 2, Placed = Placed, Days = days });
    }

    [Fact]
    public void NilAndSkippedItemsFollowIsNullable()
    {
        var order = new Order
        {
            Id = 3,
            Placed = Placed,
            Scores = [1, null, 3],
            Labels = ["a", null],
            Aliases = ["x", null],
            Stops = [new Address { City = "Graz" }, null],
        };

        AssertParity(order);
    }

    [Fact]
    public void NilWrapperForAbsentCollectionWithIsNullable()
    {
        AssertParity(new Order { Id = 4, Placed = Placed, Scores = null, Labels = null });
    }

    [Fact]
    public void BaseClassMembersComeFirst()
    {
        AssertParity(new Document { CreatedBy = "ada", Created = Placed, Title = "Notes" });
    }

    [Fact]
    public void DateOnlyAndTimeOnly()
    {
        AssertParity(new Reminder { Id = 3, Title = "Read obj/generated", DueBy = new DateOnly(2026, 9, 5), At = new TimeOnly(10, 30, 0, 123) });
        AssertParity(new Reminder { Id = 4, Title = "Whole seconds", DueBy = null, At = new TimeOnly(10, 30) });
    }

    [Fact]
    public void SelfReferencingModel()
    {
        AssertParity(new Node { Name = "root", Child = new Node { Name = "leaf" } });
    }

    [Fact]
    public void XmlShapeDescribesTheTree()
    {
        Assert.Equal(new XmlName("order", "urn:qyl:orders"), Order.XmlShape.Root);
        Assert.Equal("Order", Order.XmlShape.TypeName);
        Assert.Equal(new XmlName("document", null), Document.XmlShape.Root);
        Assert.Equal(3, Document.XmlShape.Nodes.Count);
        Assert.Equal(new XmlName("Customer", null), Customer.XmlShape.Root);

        var lines = Order.XmlShape.Nodes.OfType<XmlCollectionShape>().Single(node => node.PropertyName == "Lines");
        Assert.Equal(new XmlName("lines", null), lines.Wrapper);
        Assert.Equal("line", lines.Item.LocalName);
        Assert.Equal("OrderLine", lines.ItemShape!().TypeName);
        Assert.True(lines.ItemNillable);

        var status = Order.XmlShape.Nodes.OfType<XmlAttributeShape>().Single(node => node.PropertyName == "Status");
        Assert.Equal(StatusNames, status.Value.EnumValues!.ToArray());

        var days = Order.XmlShape.Nodes.OfType<XmlElementShape>().Single(node => node.PropertyName == "Days");
        Assert.Null(days.Value.EnumValues);

        var weight = Order.XmlShape.Nodes.OfType<XmlElementShape>().Single(node => node.PropertyName == "Weight");
        Assert.True(weight.Nillable);
        Assert.False(weight.Optional);

        var customer = Order.XmlShape.Nodes.OfType<XmlModelShape>().Single(node => node.PropertyName == "Customer");
        Assert.True(customer.Optional);
        Assert.Same(Customer.XmlShape, customer.Shape());
    }

    [Fact]
    public void ChildWritesUnderTheNameTheParentChooses()
    {
        var customer = new Customer { Name = "Ada" };

        Assert.Equal("<Customer><name>Ada</name></Customer>", customer.ToXml());

        var buffer = new StringWriter();
        using (var writer = XmlWriter.Create(buffer, Settings))
        {
            customer.WriteXml(writer, "buyer", "urn:b");
        }

        Assert.Equal("<buyer xmlns=\"urn:b\"><name>Ada</name></buyer>", buffer.ToString());
    }

    private static void AssertParity<T>(T value)
        where T : IXmlWritable
    {
        var expected = SerializeWithXmlSerializer(value);
        var actual = value.ToXml();

        Assert.Equal(expected, actual);
    }

    private static string SerializeWithXmlSerializer<T>(T value)
    {
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add(string.Empty, string.Empty);

        var buffer = new StringWriter();
        using (var writer = XmlWriter.Create(buffer, Settings))
        {
            new XmlSerializer(typeof(T)).Serialize(writer, value, namespaces);
        }

        return buffer.ToString();
    }
}
