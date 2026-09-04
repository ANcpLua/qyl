using System.Xml;

namespace Qyl.Xml;

/// <summary>A type that writes itself as an XML element. Implemented by the generator behind <see cref="GenerateXmlAttribute"/>.</summary>
public interface IXmlWritable
{
    /// <summary>Writes this instance as a complete element, including its start and end tags.</summary>
    /// <param name="writer">The writer to emit into.</param>
    void WriteXml(XmlWriter writer);
}
