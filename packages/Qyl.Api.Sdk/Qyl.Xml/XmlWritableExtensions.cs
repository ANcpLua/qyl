using System.Text;
using System.Xml;

namespace Qyl.Xml;

/// <summary>Convenience over <see cref="IXmlWritable"/>.</summary>
public static class XmlWritableExtensions
{
    private static readonly XmlWriterSettings StringSettings = new()
    {
        OmitXmlDeclaration = true,
        ConformanceLevel = ConformanceLevel.Document,
        Indent = false,
    };

    private static readonly XmlWriterSettings StreamSettings = new()
    {
        OmitXmlDeclaration = true,
        ConformanceLevel = ConformanceLevel.Document,
        Indent = false,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        CloseOutput = false,
    };

    /// <summary>Renders the value as a standalone XML document without an XML declaration.</summary>
    /// <param name="value">The value to render.</param>
    /// <returns>The document as a string.</returns>
    public static string ToXml(this IXmlWritable value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var buffer = new StringBuilder();
        using (var writer = XmlWriter.Create(buffer, StringSettings))
        {
            value.WriteXml(writer);
        }

        return buffer.ToString();
    }

    /// <summary>Writes the value as a standalone UTF-8 XML document, without declaration or byte order mark, into a stream.</summary>
    /// <param name="value">The value to write.</param>
    /// <param name="stream">The stream that receives the UTF-8 bytes; it stays open.</param>
    public static void WriteXmlTo(this IXmlWritable value, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(stream);

        using var writer = XmlWriter.Create(stream, StreamSettings);
        value.WriteXml(writer);
    }
}
