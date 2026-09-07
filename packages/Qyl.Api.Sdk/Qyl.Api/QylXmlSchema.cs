using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Qyl.Xml;

namespace Qyl;

/// <summary>
/// Turns the <see cref="XmlShape"/> a <c>[GenerateXml]</c> model exposes into the OpenAPI schema of its <c>application/xml</c>
/// document. Every model becomes one component schema, <c>{TypeName}Xml</c>, with the <c>xml</c> object describing attributes,
/// element names, wrappers, and namespaces; no reflection, only the data the generator wrote.
/// </summary>
internal static class QylXmlSchema
{
    /// <summary>The property key under which text content is described; OpenAPI has no keyword for it.</summary>
    public const string TextProperty = "#text";

    public static string SchemaId(XmlShape shape) => shape.TypeName + "Xml";

    /// <summary>Registers the component schema for <paramref name="shape"/> once and returns a reference to it.</summary>
    public static OpenApiSchemaReference Reference(OpenApiDocument document, XmlShape shape)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(shape);

        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        var id = SchemaId(shape);
        if (!document.Components.Schemas.ContainsKey(id))
        {
            // Registered before its properties are built, so a model that refers to itself resolves to this very component.
            var schema = new OpenApiSchema();
            document.Components.Schemas[id] = schema;
            Populate(schema, document, shape);
        }

        return new OpenApiSchemaReference(id, document);
    }

    private static void Populate(OpenApiSchema schema, OpenApiDocument document, XmlShape shape)
    {
        var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        var required = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in shape.Nodes)
        {
            switch (node)
            {
                case XmlAttributeShape attribute:
                    var attributeSchema = Scalar(attribute.Value, nillable: false);
                    attributeSchema.Xml = XmlOf(attribute.Name, attribute: true);
                    Add(properties, required, attribute.Name.LocalName, attributeSchema, attribute.Optional);
                    break;

                case XmlElementShape element:
                    var elementSchema = Scalar(element.Value, element.Nillable);
                    elementSchema.Xml = XmlOf(element.Name, attribute: false);
                    Add(properties, required, element.Name.LocalName, elementSchema, element.Optional);
                    break;

                case XmlTextShape text:
                    Add(properties, required, TextProperty, Scalar(text.Value, nillable: false), text.Optional);
                    break;

                case XmlModelShape model:
                    Add(properties, required, model.Name.LocalName, Child(document, model.Shape(), model.Name, model.Nillable), model.Optional);
                    break;

                case XmlCollectionShape collection:
                    var items = collection.ItemShape is { } itemShape
                        ? Child(document, itemShape(), collection.Item, collection.ItemNillable)
                        : Scalar(collection.ItemValue!, collection.ItemNillable);
                    items.Xml ??= XmlOf(collection.Item, attribute: false);
                    var array = new OpenApiSchema { Type = JsonSchemaType.Array, Items = items };
                    if (collection.Wrapper is { } wrapper)
                    {
                        array.Xml = XmlOf(wrapper, attribute: false);
                        array.Xml.Wrapped = true;
                        Add(properties, required, wrapper.LocalName, array, collection.Optional);
                    }
                    else
                    {
                        Add(properties, required, collection.Item.LocalName, array, collection.Optional);
                    }

                    break;

                default:
                    throw new InvalidOperationException($"Unknown XML shape node '{node.GetType().Name}'.");
            }
        }

        schema.Type = JsonSchemaType.Object;
        schema.Xml = XmlOf(shape.Root, attribute: false);
        schema.Properties = properties;
        schema.Required = required.Count == 0 ? null : required;
    }

    /// <summary>A nested model: its component, under the element name the parent chose.</summary>
    private static OpenApiSchema Child(OpenApiDocument document, XmlShape shape, XmlName name, bool nillable)
    {
        var child = new OpenApiSchema { AllOf = [Reference(document, shape)], Xml = XmlOf(name, attribute: false) };
        if (nillable)
        {
            child.Type = JsonSchemaType.Null | JsonSchemaType.Object;
        }

        return child;
    }

    private static OpenApiSchema Scalar(XmlScalarShape value, bool nillable)
    {
        var (type, format) = value.Type switch
        {
            XmlScalarType.String => (JsonSchemaType.String, null),
            XmlScalarType.Boolean => (JsonSchemaType.Boolean, null),
            XmlScalarType.Integer => (JsonSchemaType.Integer, "int32"),
            XmlScalarType.Long => (JsonSchemaType.Integer, "int64"),
            XmlScalarType.Float => (JsonSchemaType.Number, "float"),
            XmlScalarType.Double => (JsonSchemaType.Number, "double"),
            XmlScalarType.Decimal => (JsonSchemaType.Number, "decimal"),
            XmlScalarType.DateTime => (JsonSchemaType.String, "date-time"),
            XmlScalarType.Date => (JsonSchemaType.String, "date"),
            XmlScalarType.Time => (JsonSchemaType.String, "time"),
            XmlScalarType.Uuid => (JsonSchemaType.String, "uuid"),
            XmlScalarType.Duration => (JsonSchemaType.String, "duration"),
            XmlScalarType.Enum => (JsonSchemaType.String, null),
            _ => throw new InvalidOperationException($"Unknown XML scalar type '{value.Type}'."),
        };

        var schema = new OpenApiSchema { Type = nillable ? type | JsonSchemaType.Null : type, Format = format };
        if (value.EnumValues is { } names)
        {
            schema.Enum = [.. names.Select(static name => (JsonNode)JsonValue.Create(name))];
        }

        return schema;
    }

    private static OpenApiXml XmlOf(XmlName name, bool attribute)
    {
        var xml = new OpenApiXml { Name = name.LocalName, Attribute = attribute };

        // OpenAPI expresses only absolute namespace URIs; inheritance (null) and the empty namespace stay implicit.
        if (!string.IsNullOrEmpty(name.Namespace) && Uri.TryCreate(name.Namespace, UriKind.Absolute, out var uri))
        {
            xml.Namespace = uri;
        }

        return xml;
    }

    private static void Add(Dictionary<string, IOpenApiSchema> properties, HashSet<string> required, string key, IOpenApiSchema schema, bool optional)
    {
        properties[key] = schema;
        if (!optional)
        {
            required.Add(key);
        }
    }
}
