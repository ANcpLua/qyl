namespace Qyl.Xml;

/// <summary>An XML name. A <see langword="null"/> namespace inherits the enclosing element's namespace; an empty one is the empty namespace.</summary>
/// <param name="LocalName">The local name.</param>
/// <param name="Namespace">The namespace, <see langword="null"/> to inherit.</param>
public sealed record XmlName(string LocalName, string? Namespace);

/// <summary>The kind of text a scalar is written as; the vocabulary an OpenAPI schema needs.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The members name the scalar families a value is written as; that is their meaning.")]
public enum XmlScalarType
{
    /// <summary>Text.</summary>
    String,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary>An integer that fits 32 bits.</summary>
    Integer,

    /// <summary>An integer that needs 64 bits.</summary>
    Long,

    /// <summary>A single-precision number.</summary>
    Float,

    /// <summary>A double-precision number.</summary>
    Double,

    /// <summary>A decimal number.</summary>
    Decimal,

    /// <summary>A date and time, ISO 8601.</summary>
    DateTime,

    /// <summary>A date, ISO 8601.</summary>
    Date,

    /// <summary>A time of day, ISO 8601.</summary>
    Time,

    /// <summary>A UUID.</summary>
    Uuid,

    /// <summary>A duration, ISO 8601.</summary>
    Duration,

    /// <summary>One of a fixed set of names; see <see cref="XmlScalarShape.EnumValues"/>.</summary>
    Enum,
}

/// <summary>How a scalar is written.</summary>
/// <param name="Type">The kind of text.</param>
/// <param name="EnumValues">For <see cref="XmlScalarType.Enum"/>, the names that can be written; <see langword="null"/> for a flags enum, whose text combines names.</param>
public sealed record XmlScalarShape(XmlScalarType Type, IReadOnlyList<string>? EnumValues = null);

/// <summary>One property of a model, as the node it becomes in the element tree.</summary>
/// <param name="PropertyName">The CLR property the node is written from.</param>
public abstract record XmlShapeNode(string PropertyName);

/// <summary>A scalar written as an attribute of the model's element.</summary>
/// <param name="PropertyName">The CLR property.</param>
/// <param name="Name">The attribute name.</param>
/// <param name="Value">How the value is written.</param>
/// <param name="Optional">Whether the attribute is omitted when the value is absent.</param>
public sealed record XmlAttributeShape(string PropertyName, XmlName Name, XmlScalarShape Value, bool Optional) : XmlShapeNode(PropertyName);

/// <summary>A scalar written as a child element with text content.</summary>
/// <param name="PropertyName">The CLR property.</param>
/// <param name="Name">The element name.</param>
/// <param name="Value">How the value is written.</param>
/// <param name="Optional">Whether the element is omitted when the value is absent.</param>
/// <param name="Nillable">Whether an absent value is written as an empty element with <c>xsi:nil="true"</c>.</param>
public sealed record XmlElementShape(string PropertyName, XmlName Name, XmlScalarShape Value, bool Optional, bool Nillable) : XmlShapeNode(PropertyName);

/// <summary>A scalar written as the text content of the model's element.</summary>
/// <param name="PropertyName">The CLR property.</param>
/// <param name="Value">How the value is written.</param>
/// <param name="Optional">Whether nothing is written when the value is absent.</param>
public sealed record XmlTextShape(string PropertyName, XmlScalarShape Value, bool Optional) : XmlShapeNode(PropertyName);

/// <summary>A nested model written as a child element under the parent's chosen name.</summary>
/// <param name="PropertyName">The CLR property.</param>
/// <param name="Name">The element name.</param>
/// <param name="Shape">The child's tree, resolved lazily so self-referencing models can be described.</param>
/// <param name="Optional">Whether the element is omitted when the value is absent.</param>
/// <param name="Nillable">Whether an absent value is written as an empty element with <c>xsi:nil="true"</c>.</param>
public sealed record XmlModelShape(string PropertyName, XmlName Name, Func<XmlShape> Shape, bool Optional, bool Nillable) : XmlShapeNode(PropertyName);

/// <summary>A collection written as repeated elements, inside a wrapper element or directly under the parent.</summary>
/// <param name="PropertyName">The CLR property.</param>
/// <param name="Wrapper">The wrapper element, or <see langword="null"/> when the items repeat directly under the parent.</param>
/// <param name="Item">The element name of one item.</param>
/// <param name="ItemValue">How a scalar item is written; <see langword="null"/> when the items are models.</param>
/// <param name="ItemShape">The tree of a model item; <see langword="null"/> when the items are scalars.</param>
/// <param name="Optional">Whether nothing is written when the collection is absent.</param>
/// <param name="ItemNillable">Whether an absent item is written as an empty element with <c>xsi:nil="true"</c>.</param>
public sealed record XmlCollectionShape(
    string PropertyName,
    XmlName? Wrapper,
    XmlName Item,
    XmlScalarShape? ItemValue,
    Func<XmlShape>? ItemShape,
    bool Optional,
    bool ItemNillable) : XmlShapeNode(PropertyName);

/// <summary>The element tree a <c>[GenerateXml]</c> model writes, as data. Generated next to the writer that follows it.</summary>
/// <param name="TypeName">The model's type name, for schema identifiers.</param>
/// <param name="Root">The document element the model writes as a root, from <c>[XmlRoot]</c> or the type name.</param>
/// <param name="Nodes">The nodes under the element, in the order they are written; attributes always come first.</param>
public sealed record XmlShape(string TypeName, XmlName Root, IReadOnlyList<XmlShapeNode> Nodes);
