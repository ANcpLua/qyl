using Microsoft.OpenApi;
using Qyl.Api.Sdk.Xml.Generator.Tests.Models;

namespace Qyl.Api.Sdk.Xml.Generator.Tests;

/// <summary>The OpenAPI schema a Qyl API publishes for an application/xml response, built from the generated shape.</summary>
[TestClass]
public sealed class SchemaTests
{
    private static readonly string[] s_orderComponents = ["OrderXml", "CustomerXml", "AddressXml", "OrderLineXml", "MoneyXml"];

    private static readonly string[] s_statusNames = ["open", "paid", "Shipped"];

    [TestMethod]
    public void OrderSchemaDescribesAttributesElementsWrappersEnumsAndNestedModels()
    {
        var document = new OpenApiDocument();

        QylXmlSchema.Reference(document, Order.XmlShape);

        var schemas = document.Components!.Schemas!;
        Assert.AreSequenceEqual(s_orderComponents.Order(StringComparer.Ordinal), schemas.Keys.Order(StringComparer.Ordinal));

        var order = (OpenApiSchema)schemas["OrderXml"];
        Assert.AreEqual(JsonSchemaType.Object, order.Type);
        Assert.AreEqual("order", order.Xml!.Name);
        Assert.AreEqual(new Uri("urn:qyl:orders"), order.Xml.Namespace);

        var id = (OpenApiSchema)order.Properties!["id"];
        Assert.IsTrue(id.Xml!.Attribute);
        Assert.AreEqual(JsonSchemaType.Integer, id.Type);
        Assert.AreEqual("int32", id.Format);

        var status = (OpenApiSchema)order.Properties["status"];
        Assert.IsTrue(status.Xml!.Attribute);
        Assert.AreSequenceEqual(s_statusNames, [.. status.Enum!.Select(static value => value.GetValue<string>())]);

        var days = (OpenApiSchema)order.Properties["days"];
        Assert.AreEqual(JsonSchemaType.String, days.Type);
        Assert.IsNull(days.Enum);

        var weight = (OpenApiSchema)order.Properties["weight"];
        Assert.AreEqual(JsonSchemaType.Number | JsonSchemaType.Null, weight.Type);

        var lines = (OpenApiSchema)order.Properties["lines"];
        Assert.AreEqual(JsonSchemaType.Array, lines.Type);
        Assert.AreEqual("lines", lines.Xml!.Name);
        Assert.IsTrue(lines.Xml.Wrapped);
        var line = (OpenApiSchema)lines.Items!;
        Assert.AreEqual("line", line.Xml!.Name);
        Assert.AreEqual("OrderLineXml", ((OpenApiSchemaReference)line.AllOf![0]).Reference.Id);

        var tag = (OpenApiSchema)order.Properties["tag"];
        Assert.AreEqual(JsonSchemaType.Array, tag.Type);
        Assert.IsNull(tag.Xml);
        Assert.AreEqual(JsonSchemaType.String, ((OpenApiSchema)tag.Items!).Type);

        var counts = (OpenApiSchema)order.Properties["Counts"];
        Assert.AreEqual("int", ((OpenApiSchema)counts.Items!).Xml!.Name);

        var customer = (OpenApiSchema)order.Properties["customer"];
        Assert.AreEqual("customer", customer.Xml!.Name);
        Assert.AreEqual("CustomerXml", ((OpenApiSchemaReference)customer.AllOf![0]).Reference.Id);

        var money = (OpenApiSchema)schemas["MoneyXml"];
        Assert.AreEqual(JsonSchemaType.Number, ((OpenApiSchema)money.Properties![QylXmlSchema.TextProperty]).Type);
        Assert.IsTrue(((OpenApiSchema)money.Properties["currency"]).Xml!.Attribute);

        var required = order.Required!.ToArray();
        Assert.Contains("id", required, StringComparer.Ordinal);
        Assert.Contains("weight", required, StringComparer.Ordinal);
        Assert.DoesNotContain("customer", required, StringComparer.Ordinal);
        Assert.DoesNotContain("note", required, StringComparer.Ordinal);
    }

    [TestMethod]
    public void SelfReferencingModelResolvesToItsOwnComponent()
    {
        var document = new OpenApiDocument();

        var reference = QylXmlSchema.Reference(document, Node.XmlShape);

        Assert.AreEqual("NodeXml", reference.Reference.Id);
        var schemas = document.Components!.Schemas!;
        Assert.ContainsSingle(schemas);
        var node = (OpenApiSchema)schemas["NodeXml"];
        var child = (OpenApiSchema)node.Properties!["child"];
        Assert.AreEqual("NodeXml", ((OpenApiSchemaReference)child.AllOf![0]).Reference.Id);
    }

    [TestMethod]
    public void ReferenceIsRegisteredOnce()
    {
        var document = new OpenApiDocument();

        QylXmlSchema.Reference(document, Order.XmlShape);
        QylXmlSchema.Reference(document, Customer.XmlShape);
        QylXmlSchema.Reference(document, Order.XmlShape);

        Assert.HasCount(5, document.Components!.Schemas!);
    }
}
