using System.Xml;

namespace Qyl.Xml;

/// <summary>
/// A type that writes itself as an XML element tree. Implemented by the generator behind <see cref="GenerateXmlAttribute"/>:
/// scalars become attributes, elements, or text; nested <c>[GenerateXml]</c> models and collections become child elements.
/// </summary>
public interface IXmlWritable
{
    /// <summary>The element tree this type writes, as data: the document element and every node under it.</summary>
    static abstract XmlShape XmlShape { get; }

    /// <summary>Writes this instance as the document element, including its start and end tags.</summary>
    /// <param name="writer">The writer to emit into.</param>
    void WriteXml(XmlWriter writer);

    /// <summary>
    /// Writes this instance as a child element under a name chosen by the caller, the way a parent model writes a nested member.
    /// </summary>
    /// <param name="writer">The writer to emit into.</param>
    /// <param name="localName">The local name of the element.</param>
    /// <param name="ns">The namespace of the element; <see langword="null"/> inherits the enclosing element's namespace, an empty string is the empty namespace.</param>
    void WriteXml(XmlWriter writer, string localName, string? ns);
}
