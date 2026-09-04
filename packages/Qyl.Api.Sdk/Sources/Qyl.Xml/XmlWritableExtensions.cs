using System.Text;
using System.Xml;

namespace Qyl.Xml;

/// <summary>Convenience over <see cref="IXmlWritable"/>.</summary>
public static class XmlWritableExtensions
{
    private static readonly XmlWriterSettings DocumentSettings = new()
    {
        OmitXmlDeclaration = true,
        ConformanceLevel = ConformanceLevel.Document,
        Indent = false,
    };

    /// <summary>Renders the value as a standalone XML document without an XML declaration.</summary>
    /// <param name="value">The value to render.</param>
    /// <returns>The document as a string.</returns>
    public static string ToXml(this IXmlWritable value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var buffer = new StringBuilder();
        using (var writer = XmlWriter.Create(buffer, DocumentSettings))
        {
            value.WriteXml(writer);
        }

        return buffer.ToString();
    }
}
