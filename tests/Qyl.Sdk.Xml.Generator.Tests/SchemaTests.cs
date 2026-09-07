using Microsoft.OpenApi;
using Qyl.Sdk.Xml.Generator.Tests.Models;

namespace Qyl.Sdk.Xml.Generator.Tests;

/// <summary>The OpenAPI schema a Qyl API publishes for an application/xml response, built from the generated shape.</summary>
public sealed class SchemaTests
{
    private static readonly string[] OrderComponents = ["OrderXml", "CustomerXml", "AddressXml", "OrderLineXml", "MoneyXml"];

    private static readonly string[] StatusNames = ["open", "paid", "Shipped"];

    [Fact]
    public void OrderSchemaDescribesAttributesElementsWrappersEnumsAndNestedModels()
    {
        var document = new OpenApiDocument();

        QylXmlSchema.Reference(document, Order.XmlShape);

        var schemas = document.Components!.Schemas!;
        Assert.Equal(OrderComponents.Order(StringComparer.Ordinal), schemas.Keys.Order(StringComparer.Ordinal));

        var order = (OpenApiSchema)schemas["OrderXml"];
        Assert.Equal(JsonSchemaType.Object, order.Type);
        Assert.Equal("order", order.Xml!.Name);
        Assert.Equal(new Uri("urn:qyl:orders"), order.Xml.Namespace);

        var id = (OpenApiSchema)order.Properties!["id"];
        Assert.True(id.Xml!.Attribute);
        Assert.Equal(JsonSchemaType.Integer, id.Type);
        Assert.Equal("int32", id.Format);

        var status = (OpenApiSchema)order.Properties["status"];
        Assert.True(status.Xml!.Attribute);
        Assert.Equal(StatusNames, status.Enum!.Select(static value => value!.GetValue<string>()).ToArray());

        var days = (OpenApiSchema)order.Properties["days"];
        Assert.Equal(JsonSchemaType.String, days.Type);
        Assert.Null(days.Enum);

        var weight = (OpenApiSchema)order.Properties["weight"];
        Assert.Equal(JsonSchemaType.Number | JsonSchemaType.Null, weight.Type);

        var lines = (OpenApiSchema)order.Properties["lines"];
        Assert.Equal(JsonSchemaType.Array, lines.Type);
        Assert.Equal("lines", lines.Xml!.Name);
        Assert.True(lines.Xml.Wrapped);
        var line = (OpenApiSchema)lines.Items!;
        Assert.Equal("line", line.Xml!.Name);
        Assert.Equal("OrderLineXml", ((OpenApiSchemaReference)line.AllOf![0]).Reference.Id);

        var tag = (OpenApiSchema)order.Properties["tag"];
        Assert.Equal(JsonSchemaType.Array, tag.Type);
        Assert.Null(tag.Xml);
        Assert.Equal(JsonSchemaType.String, ((OpenApiSchema)tag.Items!).Type);

        var counts = (OpenApiSchema)order.Properties["Counts"];
        Assert.Equal("int", ((OpenApiSchema)counts.Items!).Xml!.Name);

        var customer = (OpenApiSchema)order.Properties["customer"];
        Assert.Equal("customer", customer.Xml!.Name);
        Assert.Equal("CustomerXml", ((OpenApiSchemaReference)customer.AllOf![0]).Reference.Id);

        var money = (OpenApiSchema)schemas["MoneyXml"];
        Assert.Equal(JsonSchemaType.Number, ((OpenApiSchema)money.Properties![QylXmlSchema.TextProperty]).Type);
        Assert.True(((OpenApiSchema)money.Properties["currency"]).Xml!.Attribute);

        var required = order.Required!.ToArray();
        Assert.Contains("id", required, StringComparer.Ordinal);
        Assert.Contains("weight", required, StringComparer.Ordinal);
        Assert.DoesNotContain("customer", required, StringComparer.Ordinal);
        Assert.DoesNotContain("note", required, StringComparer.Ordinal);
    }

    [Fact]
    public void SelfReferencingModelResolvesToItsOwnComponent()
    {
        var document = new OpenApiDocument();

        var reference = QylXmlSchema.Reference(document, Node.XmlShape);

        Assert.Equal("NodeXml", reference.Reference.Id);
        Assert.Equal(1, document.Components!.Schemas!.Count);
        var node = (OpenApiSchema)document.Components.Schemas["NodeXml"];
        var child = (OpenApiSchema)node.Properties!["child"];
        Assert.Equal("NodeXml", ((OpenApiSchemaReference)child.AllOf![0]).Reference.Id);
    }

    [Fact]
    public void ReferenceIsRegisteredOnce()
    {
        var document = new OpenApiDocument();

        QylXmlSchema.Reference(document, Order.XmlShape);
        QylXmlSchema.Reference(document, Customer.XmlShape);
        QylXmlSchema.Reference(document, Order.XmlShape);

        Assert.Equal(5, document.Components!.Schemas!.Count);
    }
}
